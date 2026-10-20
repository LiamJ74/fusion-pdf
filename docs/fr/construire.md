# Construire depuis les sources

[English](../en/building.md) · [Retour au README](../../README.fr.md)

## Aucun SDK

Le compilateur est le `csc.exe` livré avec Windows
(`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). Il ne connaît que le
**C# 5** : pas d'interpolation `$"…"`, pas de `?.`, pas de `nameof`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-FusionPDF.ps1
```

Le build :

1. vérifie que les sources sont en UTF-8 **avec BOM**, et que les scripts du
   paquet sont en ASCII strict et passent le parseur PowerShell 5.1 ;
2. vérifie que **chaque texte de l'interface a sa traduction anglaise** ;
3. compile dans un dossier temporaire ;
4. lance `FusionPDF.exe --autotest` : 13 contrôles sur de vrais fichiers. XAML
   dans les deux langues, tri naturel, renommage et son annulation, garde contre
   les collisions, fichiers verrouillés, et, si qpdf est installé, une vraie
   fusion, un PDF protégé et un PDF endommagé reconnus et nommés, aperçu ;
5. ouvre la fenêtre, vérifie qu'elle répond, la ferme ;
6. copie `FusionPDF.exe` et `logo.png` dans `package\`.

Rien n'est copié si une étape échoue. La version a **une seule source** :
`$VersionAttendue` dans `package\detection.ps1`.

## Comment c'est construit

| | |
|---|---|
| Interface | WPF ; `src\MainWindow.xaml` est embarqué et chargé à l'exécution par `XamlReader` (pas de MSBuild, donc pas de `x:Class`) : contrôles retrouvés par `x:Name`, événements branchés dans `Fenetre.cs` |
| Moteur | `src\Moteur.cs` : localisation de qpdf (registre de désinstallation, puis `Program Files\qpdf*`, puis `PATH`), OneDrive, verrous, tri naturel (`StrCmpLogicalW`), renommage |
| Fusion | `src\Fusion.cs` : en tâche de fond (`Task.Run`), écrit dans un fichier temporaire, garde les codes qpdf 0 et 3, relit le résultat avant de conclure |
| Aperçu | `src\Apercu.cs` : `Windows.Data.Pdf`, référencé par les `.winmd` de `C:\Windows\System32\WinMetadata`, attendu par `Completed` (les extensions `await` exigent le SDK Windows 10) |
| Langues | `src\Langue.cs` |

## Traductions

Le français est la clé. Dans le code, chaque texte affiché passe par `L("…")` ;
dans le XAML, il est entre `§…§`. `src\textes.en.txt` donne l'anglais de chaque
texte français :

```
fr: La liste est vide.
en: The list is empty.
```

`tools\Get-TextesATraduire.ps1 -Manquantes` liste les textes encore sans
traduction ; le build refuse de compiler tant qu'il y en a.

## Icône

`tools\New-Icone.ps1 -Dest resources` dessine l'icône neutre (deux pages sur une
tuile bleue). `-Logo <png>` pose votre logo, en blanc, sur la même tuile.

## Pilotes graphiques Intel

Sur certains pilotes Intel, le moteur PDF de Windows plante au déchargement du
processus (`igd10umt64xe.dll`, 0xC0000409). Fusion PDF se termine donc par
`TerminateProcess`, une fois tout écrit et fermé.
