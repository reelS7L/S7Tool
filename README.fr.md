# s7Tool

*[Read in English](README.md)*

s7Tool est une boîte à outils sysadmin pour Windows : un tableau de bord WPF regroupant un ensemble d'utilitaires natifs et légers pour le travail quotidien de technicien/administrateur — analyse de santé et d'espace disque, gestion de partitions hors ligne, scan réseau/ports, gestion des processus, quelques outils de maintenance Windows — plus un assistant IA optionnel.

Le projet privilégie fortement les API Win32 natives (P/Invoke) plutôt que des wrappers PowerShell ou WMI partout où la performance et la fiabilité comptent (accès disque, santé SMART/NVMe), donc la plupart des outils sont rapides et fonctionnent même sur des postes verrouillés.

## Projets

Le dépôt contient trois projets .NET 8 (pas de fichier solution — build chaque projet individuellement ou ouvre le dossier dans ton IDE) :

- **S7Tool** — l'application WPF principale (MVVM via CommunityToolkit.Mvvm, DI via Microsoft.Extensions.DependencyInjection). `Views/MainWindow.xaml` est le tableau de bord ; chaque outil est une fenêtre séparée résolue via DI et enregistrée dans `App.xaml.cs`.
- **S7Tool.DiskEngine** — un moteur disque natif bas niveau (P/Invoke brut, sans PowerShell/WMI) : accès disque physique brut, édition de table GPT, clonage/copie au niveau secteur, et lecture de santé SMART (ATA passthrough) / NVMe. Partagé par l'appli principale et l'outil WinPE.
- **S7Tool.DiskManagerPE** — une appli WPF distincte qui sert de shell à une image de démarrage WinPE personnalisée, utilisée pour redimensionner/cloner des partitions hors ligne quand Windows lui-même ne peut pas être touché (ex. redimensionner le volume sur lequel Windows tourne). Construite en image amorçable par `tools/Build-WinPE.ps1` (nécessite le Windows ADK sur la machine de build) et livrée pré-construite, embarquée dans l'appli principale sous `S7Tool/Resources/WinPE/`.

## Outils

- **Task Manager** — liste et surveillance des processus.
- **Scanner réseau** — découverte des hôtes sur le réseau local.
- **Scanner de ports** — scan de ports TCP.
- **Santé des disques** — tableau de bord SMART (ATA) et NVMe, lu en passthrough brut, sans WMI.
- **Analyseur d'espace disque** — vues liste, carrés imbriqués (treemap) et diagramme circulaire de l'utilisation disque, chacune avec un menu contextuel clic droit (ouvrir/explorateur/copier le chemin/actualiser/propriétés/supprimer).
- **Désinstallateur** — suppression d'applications installées.
- **Installateur d'applications** — parcours une liste organisée d'applications populaires ou cherche dans le catalogue winget, coche celles voulues, et installe le tout silencieusement via winget.
- **Renommer le PC** — gestion du nom de la machine.
- **Windows Update Manager** — contrôle de Windows Update via l'interface COM WUApi.
- **Désactiver le démarrage rapide** — bascule en un clic.
- **Gestionnaire de disques WinPE** — démarre une image WinPE embarquée pour redimensionner/cloner des partitions hors ligne, y compris le volume système ; retour visuel en direct pour que la barre d'espace libre adjacente se réduise correctement pendant le glisser pour étendre une partition.
- **Assistant IA** — chat propulsé par Google Gemini, avec ta propre clé API (rien n'est embarqué ni proxifié).

L'interface de l'appli elle-même peut basculer entre français et anglais à l'exécution grâce au petit bouton de langue à côté du logo sur le tableau de bord.

## Prérequis

- Windows 10/11, x64.
- SDK .NET 8 pour build depuis les sources (la release publiée est autonome et ne nécessite rien d'installé).
- Droits administrateur à l'exécution pour la plupart des outils (accès disque brut, scan réseau, configuration système).
- Windows ADK uniquement si tu comptes reconstruire toi-même l'image WinPE via `tools/Build-WinPE.ps1`.

## Build

Chaque projet peut être build indépendamment avec le SDK .NET, par exemple :

```
dotnet build S7Tool/S7Tool.csproj
dotnet build S7Tool.DiskManagerPE/S7Tool.DiskManagerPE.csproj
```

`S7Tool.DiskEngine` est référencé comme dépendance de projet par les deux applis WPF et n'a pas besoin d'être build séparément.

Pour publier un build autonome de l'appli principale :

```
dotnet publish S7Tool/S7Tool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

`Resources/WinPE/boot.wim` et `boot.sdi` sont suivis via Git LFS (ce sont des binaires pré-construits, pas des sources) — assure-toi d'avoir fait un `git lfs pull` avant de build, sinon la fonctionnalité Gestionnaire de disques WinPE n'aura pas son image de démarrage.

## Releases

Des binaires pré-construits et autonomes sont publiés dans les [Releases](../../releases) — télécharge, extrais, et lance `S7Tool.exe`. Pas d'installeur, pas de dépendances.

## Licence

Aucune licence n'a encore été choisie pour ce projet.
