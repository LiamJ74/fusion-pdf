<p align="center">
  <img src="docs/img/icon.png" width="96" alt="Fusion PDF">
</p>

<h1 align="center">Fusion PDF</h1>

<p align="center">
  <b>Assemblez plusieurs PDF en un seul fichier, dans l'ordre de votre choix.</b><br>
  Une application de bureau Windows toute simple, en français et en anglais, construite au-dessus de <a href="https://github.com/qpdf/qpdf">qpdf</a>.
</p>

<p align="center">
  <a href="README.md">English</a> · <b>Français</b>
</p>

> **Outil non officiel.** Fusion PDF est un projet indépendant, sans lien avec le
> projet qpdf, qui ne l'approuve ni ne le maintient. Il se contente d'appeler le
> programme en ligne de commande `qpdf`, à installer séparément.

![Fusion PDF](docs/img/fr-main.png)

## Pourquoi

Fusionner des PDF ne devrait demander ni script, ni console, ni l'envoi de
documents confidentiels sur un site web. Fusion PDF offre une fenêtre claire :
on dépose les fichiers, on vérifie l'ordre, on clique sur **Fusionner**. Les
pages sont copiées telles quelles par qpdf : **aucune perte de qualité**, et
150 fichiers se fusionnent en quelques secondes.

## Fonctionnalités

- **Glisser-déposer** de PDF ou de dossiers entiers, ou clic droit sur un dossier
  dans l'Explorateur : *Fusionner les PDF de ce dossier*.
- **Réordonner** en glissant les lignes, ou avec *Monter / Descendre*, *Trier par
  nom* (tri naturel : `page9` avant `page10`), *Trier par date*, *Inverser*.
- **Aperçu de la première page** du fichier sélectionné (moteur PDF de Windows).
- **Nombre de pages et taille** de chaque fichier et de la liste entière.
- **Fichiers OneDrive « en ligne seulement »** : repérés et téléchargés automatiquement avant la fusion.
- **Messages d'erreur en clair**, qui nomment le fichier en cause : protégé par mot
  de passe, endommagé, verrouillé par un autre programme, dossier vide, qpdf absent.
- **Renommage facultatif** des sources `001_`, `002_`… dans l'ordre de la liste,
  **réversible** grâce à un journal CSV (*Annuler un renommage…*).
- **Fenêtre réactive pendant la fusion** : elle tourne en tâche de fond, avec une barre de progression.
- **Français et anglais**, selon Windows, avec un sélecteur **FR | EN** dans la fenêtre.
- **Aide intégrée** (F1), affichée d'office au premier lancement.
- **Aucun environnement à déployer** : un seul `.exe` de 200 Ko sur .NET Framework 4.8,
  livré avec Windows 10 et 11.

## Prérequis

| | |
|---|---|
| Windows | 10 ou 11, x64 |
| .NET | Framework 4.8 (inclus dans Windows 10 1903+ et Windows 11) |
| qpdf | installé à part : `winget install QPDF.QPDF` |

## Démarrage rapide

1. Installez qpdf : `winget install QPDF.QPDF`
2. Téléchargez `FusionPDF.exe` depuis la [dernière version](../../releases/latest) et lancez-le.
   Pour avoir le raccourci du menu Démarrer et le clic droit pour tous les
   utilisateurs, lancez plutôt `package\install.ps1` en administrateur (voir le
   [guide de déploiement](docs/fr/deploiement.md)).
3. Déposez vos PDF, vérifiez l'ordre, cliquez sur **Fusionner**.

## Documentation

| | Français | English |
|---|---|---|
| Guide utilisateur | [guide-utilisateur.md](docs/fr/guide-utilisateur.md) | [user-guide.md](docs/en/user-guide.md) |
| Déploiement (Intune, scripts) | [deploiement.md](docs/fr/deploiement.md) | [deployment.md](docs/en/deployment.md) |
| Construire depuis les sources | [construire.md](docs/fr/construire.md) | [building.md](docs/en/building.md) |
| Dépannage | [depannage.md](docs/fr/depannage.md) | [troubleshooting.md](docs/en/troubleshooting.md) |

## Organisation du dépôt

```
src/         sources C# 5 / WPF, XAML, traductions anglaises (textes.en.txt)
resources/   icône et tuiles
package/     install.ps1, detection.ps1, uninstall.ps1 (SYSTEM, prêts pour Intune)
intune/      paquet qpdf, construction des .intunewin, publication par Graph (plan / -Apply)
tools/       Build-FusionPDF.ps1, New-Icone.ps1, Get-TextesATraduire.ps1
docs/        guides utilisateur, déploiement et construction, en français et en anglais
```

## Licence

[Apache License 2.0](LICENSE). qpdf est un programme distinct, sous sa propre
licence (Apache 2.0 elle aussi) ; il n'est pas inclus dans ce dépôt. Voir [NOTICE](NOTICE).
