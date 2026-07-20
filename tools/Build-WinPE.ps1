<#
.SYNOPSIS
    Construit l'image WinPE personnalisée embarquée par S7Tool — devient le shell complet
    de l'environnement hors ligne (gestion de disques : détection, CRUD, clonage, déplacement).

.DESCRIPTION
    Script de DÉVELOPPEMENT UNIQUEMENT — jamais exécuté sur le poste d'un technicien. Nécessite le
    Windows ADK + module complémentaire WinPE installés sur CE poste de build (pas sur les postes
    cibles : le résultat de ce script, boot.wim + boot.sdi, est ensuite committé dans
    S7Tool\Resources\WinPE\ et distribué tel quel avec l'application — les postes cibles
    n'ont besoin de rien installer.

    À relancer uniquement quand S7Tool.DiskManagerPE change (nouvelle fonctionnalité,
    correctif) et qu'il faut donc regénérer l'image embarquée avec la version à jour de l'outil.
    Aucun paquet DISM optionnel (WinPE-WMI/PowerShell/StorageWMI...) n'est nécessaire : diskpart et
    format sont déjà présents dans toute image WinPE de base, et l'outil ne s'appuie que dessus +
    sur l'accès disque bas niveau natif de S7Tool.DiskEngine.

.PARAMETER AppPublishDir
    Dossier de publication self-contained (dotnet publish, win-x64, single-file) de
    S7Tool.DiskManagerPE — copié intégralement dans l'image (l'exe seul ne suffit pas :
    les DLL natives WPF ne peuvent pas être embarquées dans le single-file).

.PARAMETER OutputDir
    Dossier où copier boot.wim et boot.sdi une fois construits (typiquement
    S7Tool\Resources\WinPE\ dans le dépôt).
#>
param(
    [Parameter(Mandatory = $true)][string]$AppPublishDir,
    [Parameter(Mandatory = $true)][string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$appExePath = Join-Path $AppPublishDir 'S7Tool.DiskManagerPE.exe'
if (-not (Test-Path $appExePath)) { throw "Introuvable : $appExePath (publie d'abord S7Tool.DiskManagerPE en self-contained win-x64)." }

$copype = 'C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Windows Preinstallation Environment\copype.cmd'
$dandiSetEnv = 'C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\DandISetEnv.bat'
if (-not (Test-Path $copype)) { throw "Windows ADK + module WinPE introuvables sur CE poste de build ($copype)." }
if (-not (Test-Path $dandiSetEnv)) { throw "Script d'environnement ADK introuvable ($dandiSetEnv)." }

$buildDir = 'C:\S7Tool\WinPEBuildTmp'
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# copype.cmd a besoin des variables d'environnement posees par DandISetEnv.bat (WinPERoot,
# OSCDImgRoot...) pour retrouver les fichiers de l'ADK — les deux doivent tourner dans le MEME
# process cmd.exe, sinon les variables posees par le premier ne survivent pas jusqu'au second.
Write-Output 'Creation de la structure WinPE de base (copype)...'
cmd /c "`"$dandiSetEnv`" && `"$copype`" amd64 `"$buildDir`""
if ($LASTEXITCODE -ne 0) { throw "copype a echoue (code $LASTEXITCODE)" }

$mountDir = Join-Path $buildDir 'mount'
$wimPath = Join-Path $buildDir 'media\sources\boot.wim'
New-Item -ItemType Directory -Path $mountDir -Force | Out-Null

Write-Output 'Montage de l''image WinPE...'
dism /Mount-Image /ImageFile:"$wimPath" /Index:1 /MountDir:"$mountDir"
if ($LASTEXITCODE -ne 0) { throw "Montage DISM echoue (code $LASTEXITCODE)" }

try {
    Write-Output 'Injection du gestionnaire de disques hors ligne...'
    $appDestDir = "$mountDir\S7Tool\App"
    New-Item -ItemType Directory -Path $appDestDir -Force | Out-Null
    Copy-Item "$AppPublishDir\*" $appDestDir -Recurse -Force

    # winpeshl.ini lance directement notre exe (executable reel, CreateProcess brut) : plus besoin
    # de passer par cmd.exe ni par un script intermediaire. wpeinit (monte les disques internes et
    # leur assigne une lettre) est appele par l'appli elle-meme au demarrage (App.xaml.cs), pour
    # qu'aucune fenetre de console ne soit jamais visible avant l'interface graphique.
    Set-Content -Path "$mountDir\Windows\System32\winpeshl.ini" -Value "[LaunchApps]`r`nX:\S7Tool\App\S7Tool.DiskManagerPE.exe" -Encoding ASCII
}
finally {
    Write-Output 'Fermeture et validation de l''image WinPE...'
    dism /Unmount-Image /MountDir:"$mountDir" /Commit
}

Copy-Item (Join-Path $buildDir 'media\sources\boot.wim') (Join-Path $OutputDir 'boot.wim') -Force
Copy-Item (Join-Path $buildDir 'media\boot\boot.sdi') (Join-Path $OutputDir 'boot.sdi') -Force

# Sert uniquement a detecter cote Windows qu'une mise a jour de l'image est disponible
# (DiskManagerService.IsOfflineEnvironmentUpdateAvailableAsync) : compare a la version deja
# installee sur le poste, sans avoir a comparer les .wim (volumineux) directement. Horodatage de
# build plutot que FileVersion de l'exe : celle-ci reste 1.0.0.0 d'un build a l'autre, ce qui
# rendait toute mise a jour invisible (versions installee et embarquee toujours "identiques").
$imageVersion = Get-Date -Format 'yyyyMMdd-HHmmss'
Set-Content -Path (Join-Path $OutputDir 'version.txt') -Value $imageVersion -Encoding ASCII -NoNewline

Write-Output "Image WinPE prete : $OutputDir\boot.wim, boot.sdi et version.txt (version $imageVersion)"
