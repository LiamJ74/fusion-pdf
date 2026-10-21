# Déploiement

[English](../en/deployment.md) · [Retour au README](../../README.fr.md)

Trois façons d'installer Fusion PDF, de la plus simple à la plus encadrée.

## 1. L'exécutable seul

Téléchargez `FusionPDF.exe` depuis les versions publiées et lancez-le. Il n'a
besoin que de qpdf (`winget install QPDF.QPDF`). Ni raccourci dans le menu
Démarrer, ni clic droit. Un fichier téléchargé porte la marque « provenant
d'Internet » : SmartScreen peut avertir au premier lancement, car l'exécutable
n'est pas signé.

## 2. Pour tout le poste, par script

Depuis le zip de la version (ou depuis `package\` après un build), dans un
PowerShell **ouvert en administrateur** :

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

`install.ps1` installe, pour tous les utilisateurs :

| | |
|---|---|
| Exécutable | `C:\Program Files\Fusion PDF\FusionPDF.exe` |
| Menu Démarrer | `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Fusion PDF.lnk` |
| Clic droit sur un dossier | `HKLM\SOFTWARE\Classes\Directory\shell\FusionPDF` (`"%1"`) |
| Clic droit dans un dossier | `HKLM\SOFTWARE\Classes\Directory\Background\shell\FusionPDF` (`"%V"`) |
| Idem, avec les sous-dossiers | `…\Directory\shell\FusionPDFSousDossiers` et `…\Directory\Background\shell\FusionPDFSousDossiers` (`--sous-dossiers "%1"` / `"%V"`) |
| Journal | `C:\ProgramData\FusionPDF\FusionPDF-install.log` (ou `%LOCALAPPDATA%\FusionPDF\` sans droits administrateur) |

Le libellé du clic droit suit la langue d'installation de Windows. Le script
conclut sur **l'état relu** (version de l'exécutable, raccourci, les deux clés),
pas sur les opérations tentées : code 0 seulement si tout est en place.

`uninstall.ps1` ferme les fenêtres ouvertes (attente bornée), retire tout, relit,
et rend 0 seulement s'il ne reste rien. qpdf n'est pas désinstallé.

`detection.ps1` écrit une seule ligne et rend 0 quand l'exécutable est présent en
version `$VersionAttendue` ou plus récente **et** que les deux clés de clic droit existent.

## 3. Microsoft Intune (application Win32)

### Avec les scripts du dossier `intune\`

| | |
|---|---|
| `intune\qpdf\` | un paquet qpdf générique : `install.ps1` (winget `QPDF.QPDF`, pour toute la machine, en SYSTEM), `detection.ps1`, `uninstall.ps1` |
| `intune\New-Intunewin.ps1` | construit `dist\intune\FusionPDF.intunewin` et `dist\intune\qpdf.intunewin`, et compare la taille déclarée de chaque paquet à son dossier source |
| `intune\Publish-FusionPDF.ps1` | crée les applications par Microsoft Graph, pose qpdf en dépendance à installation automatique (relue avant toute suite), envoie le logo, autorise la désinstallation disponible, assigne en Disponible. **Plan par défaut** : rien n'est écrit sans `-Apply` |

```powershell
# 1. construire l'exécutable (package\FusionPDF.exe, package\logo.png)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-FusionPDF.ps1

# 2. construire les deux .intunewin (Microsoft Win32 Content Prep Tool, non inclus)
.\intune\New-Intunewin.ps1 -IntuneWinAppUtil C:\Outils\IntuneWinAppUtil.exe

# 3. publier (PowerShell 7, module Microsoft.Graph.Authentication)
.\intune\Publish-FusionPDF.ps1 -GroupId <id-du-groupe-entra>            # plan
.\intune\Publish-FusionPDF.ps1 -GroupId <id-du-groupe-entra> -Apply     # écriture
```

Vous avez déjà une application qpdf dans Intune ? Passez `-QpdfAppId <son id>` :
elle sert de dépendance au lieu d'en créer une nouvelle. Le script refuse de
toucher à une application existante du même nom.

### À la main, dans la console Intune

1. Construisez le `.intunewin` à partir du dossier `package\` (exécutable compris),
   avec `install.ps1` comme fichier d'installation. Faites-le **hors** de tout
   dossier synchronisé (OneDrive) : un empaquetage interrompu peut continuer
   d'écrire dans le dossier source.
2. Créez une application Win32 :

   | | |
   |---|---|
   | Commande d'installation | `powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File install.ps1` |
   | Commande de désinstallation | `powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -File uninstall.ps1` |
   | Contexte d'installation | Système |
   | Détection | script personnalisé `detection.ps1`, en 64 bits |
   | Logo | `package\logo.png` (produit par le build) |

3. **Dépendance** : créez (ou reprenez) une application Win32 pour qpdf, par
   exemple winget `QPDF.QPDF`, et ajoutez-la en dépendance de Fusion PDF avec
   **installation automatique**. Intune installe alors qpdf d'abord, même pour
   des utilisateurs hors de l'assignation de qpdf.
4. Assignez en **Disponible** aux utilisateurs concernés.

À savoir, tout constaté en production :

- **Intune lance les scripts d'installation en PowerShell 32 bits.** `$env:ProgramFiles`
  y vaut `Program Files (x86)` et `HKLM:` est redirigé vers `WOW6432Node`. Les
  scripts utilisent `$env:ProgramW6432` et la vue de registre 64 bits explicite :
  sans conséquence.
- **« Désinstaller » dans le Portail d'entreprise** n'apparaît, pour une Win32
  disponible, que si *Autoriser la désinstallation disponible* est à **Oui**.
- **Changer la règle de détection d'une application publiée** fait voir « non
  installée » chez ceux qui ont l'ancienne version, et une application
  *disponible* n'est pas réinstallée d'office. Publiez chaque nouvelle version
  comme **une nouvelle application qui remplace la précédente**, avec mise à jour automatique.
- **Un `PATCH` Graph sur l'application sans `largeIcon`** peut effacer son icône.
- **Menu contextuel de Windows 11** : les verbes classiques sont sous *Afficher
  d'autres options* (ou directement avec Maj + clic droit). Le premier menu exige
  une extension `IExplorerCommand` empaquetée et **signée**.

## Journaux

| | |
|---|---|
| Installation, désinstallation | `C:\ProgramData\FusionPDF\FusionPDF-*.log` en administrateur ou SYSTEM, sinon `%LOCALAPPDATA%\FusionPDF\` |
| Application (par utilisateur) | `%LOCALAPPDATA%\FusionPDF\FusionPDF.log` : sortie complète de qpdf et erreurs inattendues |
| Choix de langue (par utilisateur) | `%LOCALAPPDATA%\FusionPDF\langue.txt` |

## Ligne de commande

```
FusionPDF.exe                         fenêtre vide
FusionPDF.exe "<dossier>"             fenêtre remplie avec les PDF du dossier
FusionPDF.exe --sous-dossiers "<dossier>"  idem, avec toute l'arborescence (préférence inchangée)
FusionPDF.exe "<a.pdf>" "<b.pdf>"     fenêtre remplie avec ces fichiers, dans cet ordre
FusionPDF.exe --lang fr|en ...        impose la langue
FusionPDF.exe --autotest <rapport>    contrôles sans fenêtre (utilisé par le build)
```
