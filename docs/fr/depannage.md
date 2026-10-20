# Dépannage

[English](../en/troubleshooting.md) · [Retour au README](../../README.fr.md)

Chaque message nomme le fichier en cause, et sa ligne est sélectionnée dans la
liste. Rien n'est écrit sous le nom final quand une fusion échoue.

| Message | Que faire |
|---|---|
| **qpdf, le moteur de fusion, n'est pas installé** | Installez qpdf (`winget install QPDF.QPDF`, ou votre Portail d'entreprise), puis cliquez de nouveau sur **Fusionner** : inutile de rouvrir l'application. La pastille en haut à droite passe au vert : *qpdf x.y.z prêt*. |
| **Le fichier … est protégé par un mot de passe** | qpdf ne peut pas l'ouvrir. Retirez-le de la liste, ou ouvrez-le avec son mot de passe et enregistrez-en une copie sans protection (Imprimer › *Microsoft Print to PDF*), puis ajoutez cette copie. |
| **Le fichier … est endommagé ou n'est pas un vrai PDF** | Ouvrez-le pour vérifier, retirez-le de la liste, relancez. Un PDF seulement un peu abîmé est accepté : le bandeau de fin le signale, et le résultat est lisible. |
| **Le fichier … est ouvert dans un autre programme** / **verrouillé** | Fermez-le dans votre lecteur PDF (ou le programme qui l'utilise). Avec *Renommer* coché, aucun fichier n'est renommé tant que tous ne sont pas libres. |
| **Le dossier … ne contient aucun PDF** | Par défaut, seuls les PDF placés directement dans le dossier sont pris. Si ses sous-dossiers en contiennent, un bandeau bleu l'indique : cochez **Inclure les sous-dossiers**. Un message rouge signifie qu'il n'y a de PDF ni dans le dossier, ni dans ses sous-dossiers. |
| **OneDrive n'a pas pu télécharger …** | Vérifiez la connexion et que OneDrive est démarré. Sinon : clic droit sur le dossier › *Toujours conserver sur cet appareil*. |
| **Le fichier de sortie … est ouvert dans un autre programme** | Fermez le fichier fusionné précédent, ou changez le nom de sortie. |
| **Vous n'avez pas le droit d'écrire dans ce dossier** | Choisissez un autre emplacement avec **Enregistrer sous…**. |
| Pas de *Fusionner les PDF de ce dossier* dans le clic droit | Sous Windows 11, il est dans *Afficher d'autres options* (ou Maj + clic droit). S'il n'y est pas non plus, la commande n'a pas été installée : utilisez `package\install.ps1`, ou le menu Démarrer et le glisser-déposer. |
| SmartScreen avertit au premier lancement | L'exécutable n'est pas signé et a été téléchargé. L'installer par script ou par Intune dans Program Files évite l'avertissement. |

## Où regarder

- Journal de l'application, par utilisateur : `%LOCALAPPDATA%\FusionPDF\FusionPDF.log`.
  La sortie complète de qpdf et toute erreur inattendue y sont ; l'utilisateur ne
  voit qu'un message en clair.
- Codes de sortie de qpdf : **0** parfait, **3** avertissements (résultat gardé,
  lisible) ; tout autre code est un échec, et la sortie temporaire est supprimée.
- Installation : `C:\ProgramData\FusionPDF\FusionPDF-install.log`.
