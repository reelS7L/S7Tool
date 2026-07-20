namespace S7Tool.DiskEngine;

public static class SmartAttributeCatalog
{
    private static readonly Dictionary<byte, string> Names = new()
    {
        [0x01] = "Taux d'erreur de lecture brut",
        [0x02] = "Temps de mise en route",
        [0x03] = "Temps de montée en régime",
        [0x04] = "Nombre de démarrages/arrêts",
        [0x05] = "Secteurs réalloués",
        [0x07] = "Taux d'erreur de recherche",
        [0x08] = "Temps de recherche moyen",
        [0x09] = "Heures de fonctionnement",
        [0x0A] = "Nombre de tentatives de réinitialisation",
        [0x0B] = "Nombre d'événements de recalibration",
        [0x0C] = "Nombre de mises sous tension",
        [0xAB] = "Erreurs de programme/vie du SSD",
        [0xAC] = "Erreurs d'effacement",
        [0xAD] = "Taux d'usure moyen (wear leveling)",
        [0xAE] = "Nombre d'événements hors ligne inattendus",
        [0xB1] = "Usure (wear leveling count)",
        [0xB7] = "Erreurs de mode SATA",
        [0xB8] = "Erreurs de somme de contrôle (end-to-end)",
        [0xBB] = "Secteurs corrigés par ECC brut",
        [0xBE] = "Température (airflow)",
        [0xC0] = "Compteur de mise hors tension d'urgence",
        [0xC1] = "Nombre de chargements/déchargements",
        [0xC2] = "Température",
        [0xC3] = "Nombre d'erreurs de conversion Hardware ECC",
        [0xC4] = "Nombre d'événements de réallocation",
        [0xC5] = "Nombre de secteurs en attente",
        [0xC6] = "Nombre de secteurs illisibles (offline)",
        [0xC7] = "Nombre d'erreurs CRC (câble SATA)",
        [0xC8] = "Erreurs de re-transmission (write)",
        [0xCA] = "Taux de correction ECC brut",
        [0xDE] = "Débit de transfert moyen",
        [0xE8] = "Endurance restante (SSD)",
        [0xE9] = "Total secteurs écrits (SSD)",
        [0xF1] = "Total LBA écrites",
        [0xF2] = "Total LBA lues",
    };

    public static string GetName(byte id) => Names.TryGetValue(id, out var name) ? name : $"Attribut 0x{id:X2}";

    public static readonly HashSet<byte> CriticalIds = new() { 0x05, 0xC5, 0xC6, 0xBB, 0xB8 };
}
