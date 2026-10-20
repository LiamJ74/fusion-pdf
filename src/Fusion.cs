// Fusion PDF - une ligne de la liste, et le déroulé d'une fusion.
//
// Le déroulé tourne en tâche de fond. Il modifie les propriétés des lignes
// directement : WPF relaie seul un PropertyChanged venu d'un autre fil pour
// une liaison de propriété. Seule la COLLECTION doit être touchée sur le fil
// de l'interface, et le déroulé n'y touche jamais.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FusionPDF
{
    public sealed class ElementPdf : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        string chemin;
        int pages = -1;
        EtatPdf etat = EtatPdf.Lecture;
        int rang;
        bool ligneAvant, ligneApres;

        public long Taille { get; private set; }
        public DateTime Date { get; private set; }

        /// <summary>Aperçu PNG de la première page, gardé en mémoire une fois calculé.</summary>
        public byte[] Apercu;

        public ElementPdf(string chemin)
        {
            this.chemin = chemin;
            Rafraichir();
        }

        public void Rafraichir()
        {
            try
            {
                FileInfo fi = new FileInfo(chemin);
                Taille = fi.Length;
                Date = fi.LastWriteTime;
            }
            catch { }
            if (OneDrive.EstEnLigneSeulement(chemin)) etat = EtatPdf.EnLigne;
            else if (etat == EtatPdf.EnLigne) etat = pages >= 0 ? EtatPdf.Pret : EtatPdf.Lecture;
            ToutSignaler();
        }

        public string Chemin
        {
            get { return chemin; }
            set { chemin = value; Apercu = null; ToutSignaler(); }
        }

        public string Nom { get { return Path.GetFileName(chemin); } }
        public string Dossier { get { return Path.GetDirectoryName(chemin); } }

        public int Pages
        {
            get { return pages; }
            set { pages = value; ToutSignaler(); }
        }

        public EtatPdf Etat
        {
            get { return etat; }
            set { etat = value; ToutSignaler(); }
        }

        public int Rang
        {
            get { return rang; }
            set { rang = value; Signaler("TexteRang"); }
        }

        public bool LigneAvant
        {
            get { return ligneAvant; }
            set { if (ligneAvant != value) { ligneAvant = value; Signaler("VisLigneAvant"); } }
        }

        public bool LigneApres
        {
            get { return ligneApres; }
            set { if (ligneApres != value) { ligneApres = value; Signaler("VisLigneApres"); } }
        }

        // --- affichage ---------------------------------------------------

        public string TexteRang { get { return rang.ToString(CultureInfo.InvariantCulture); } }
        public string TexteTaille { get { return Format.Taille(Taille); } }
        public string InfoBulle { get { return chemin; } }

        public string TextePages
        {
            get
            {
                if (etat == EtatPdf.Lecture) return "…";
                if (pages >= 0) return Format.Pages(pages);
                return "—";
            }
        }

        public string SousTitre
        {
            get
            {
                string date = Langue.L("modifié le {0:dd/MM/yyyy 'à' HH:mm}", Date);
                string dossier = Path.GetFileName(Dossier.TrimEnd('\\'));
                return string.IsNullOrEmpty(dossier) ? date : date + "  ·  " + dossier;
            }
        }

        public Visibility VisBadge { get { return TexteBadge.Length > 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility VisLigneAvant { get { return ligneAvant ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility VisLigneApres { get { return ligneApres ? Visibility.Visible : Visibility.Collapsed; } }

        public string TexteBadge
        {
            get
            {
                switch (etat)
                {
                    case EtatPdf.EnLigne: return Langue.L("En ligne seulement");
                    case EtatPdf.Protege: return Langue.L("Protégé par mot de passe");
                    case EtatPdf.Endommage: return Langue.L("Illisible");
                    case EtatPdf.Verrouille: return Langue.L("Ouvert ailleurs");
                    case EtatPdf.Introuvable: return Langue.L("Introuvable");
                    default: return "";
                }
            }
        }

        public string IconeBadge
        {
            get
            {
                switch (etat)
                {
                    case EtatPdf.EnLigne: return "";      // nuage
                    case EtatPdf.Protege: return "";      // cadenas
                    case EtatPdf.Verrouille: return "";
                    default: return "";                   // avertissement
                }
            }
        }

        bool EnErreur
        {
            get { return etat == EtatPdf.Protege || etat == EtatPdf.Endommage || etat == EtatPdf.Introuvable; }
        }

        public Brush FondBadge
        {
            get { return EnErreur ? Couleurs.Rouge50 : etat == EtatPdf.Verrouille ? Couleurs.Ambre50 : Couleurs.P50; }
        }

        public Brush BordBadge
        {
            get { return EnErreur ? Couleurs.Rouge200 : etat == EtatPdf.Verrouille ? Couleurs.Ambre200 : Couleurs.P200; }
        }

        public Brush EncreBadge
        {
            get { return EnErreur ? Couleurs.Rouge700 : etat == EtatPdf.Verrouille ? Couleurs.Ambre800 : Couleurs.P700; }
        }

        public Brush CouleurIcone
        {
            get { return EnErreur ? Couleurs.Rouge600 : Couleurs.P500; }
        }

        /// <summary>Ce que lisent les lecteurs d'écran (et l'automatisation de l'interface).</summary>
        public override string ToString()
        {
            return Nom;
        }

        void Signaler(string nom)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(nom));
        }

        void ToutSignaler()
        {
            // Chaîne vide : toutes les propriétés ont changé.
            Signaler(string.Empty);
        }
    }

    /// <summary>Palette bleue (primary 50 a 900) et gris Tailwind.</summary>
    public static class Couleurs
    {
        public static readonly Brush P50 = Pinceau("#EFF6FF");
        public static readonly Brush P100 = Pinceau("#DBEAFE");
        public static readonly Brush P200 = Pinceau("#BFDBFE");
        public static readonly Brush P300 = Pinceau("#93C5FD");
        public static readonly Brush P500 = Pinceau("#2563EB");
        public static readonly Brush P700 = Pinceau("#1E40AF");
        public static readonly Brush Gris50 = Pinceau("#F9FAFB");
        public static readonly Brush Gris200 = Pinceau("#E5E7EB");
        public static readonly Brush Vert50 = Pinceau("#F0FDF4");
        public static readonly Brush Vert200 = Pinceau("#BBF7D0");
        public static readonly Brush Vert600 = Pinceau("#16A34A");
        public static readonly Brush Vert800 = Pinceau("#166534");
        public static readonly Brush Rouge50 = Pinceau("#FEF2F2");
        public static readonly Brush Rouge200 = Pinceau("#FECACA");
        public static readonly Brush Rouge600 = Pinceau("#DC2626");
        public static readonly Brush Rouge700 = Pinceau("#B91C1C");
        public static readonly Brush Rouge800 = Pinceau("#991B1B");
        public static readonly Brush Ambre50 = Pinceau("#FFFBEB");
        public static readonly Brush Ambre200 = Pinceau("#FDE68A");
        public static readonly Brush Ambre600 = Pinceau("#D97706");
        public static readonly Brush Ambre800 = Pinceau("#92400E");

        static Brush Pinceau(string hex)
        {
            SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();   // utilisable depuis n'importe quel fil
            return b;
        }
    }

    public sealed class ResultatFusion
    {
        public string Sortie;
        public int Pages;
        public int PagesAttendues;
        public int Fichiers;
        public bool Avertissements;
        public string Csv;
        public int Renommes;
        public ErreurUtilisateur ErreurRenommage;
        public Dictionary<string, string> CheminsRenommes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class Fusion
    {
        /// <summary>Délai de la fusion elle-même : très au-delà du réel (quelques secondes pour 150 fichiers).</summary>
        public const int DelaiFusionMs = 30 * 60 * 1000;

        static string Nom(ElementPdf e) { return L("« {0} »", e.Nom); }
        static string L(string fr) { return Langue.L(fr); }
        static string L(string fr, params object[] v) { return Langue.L(fr, v); }

        /// <summary>
        /// Déroulé complet, en tâche de fond. progres(pourcentage, texte).
        ///   0-30 %   contrôle de chaque fichier, téléchargement OneDrive si besoin
        ///   30-95 %  fusion par qpdf (progression lue sur sa sortie)
        ///   95-100 % relecture du résultat, puis renommage éventuel
        /// </summary>
        public static ResultatFusion Executer(Qpdf qpdf, IList<ElementPdf> ordre, string sortie, bool renommer,
                                              Action<double, string> progres)
        {
            int n = ordre.Count;
            Journal.Ecrire("Fusion demandée : " + n + " fichier(s) -> " + sortie + (renommer ? " (avec renommage)" : ""));

            for (int i = 0; i < n; i++)
            {
                ElementPdf e = ordre[i];
                string pos = " (" + (i + 1) + "/" + n + ")";
                progres(30.0 * i / n, L("Vérification de {0}", Nom(e)) + pos);

                if (!File.Exists(e.Chemin))
                {
                    e.Etat = EtatPdf.Introuvable;
                    throw new ErreurUtilisateur(L("Le fichier {0} est introuvable.", Nom(e)),
                                                L("Il a peut-être été déplacé, renommé ou supprimé. Retirez-le de la liste, puis relancez la fusion."));
                }

                if (OneDrive.EstEnLigneSeulement(e.Chemin))
                {
                    progres(30.0 * i / n, L("Téléchargement depuis OneDrive : {0}", Nom(e)) + pos);
                    try
                    {
                        OneDrive.Telecharger(e.Chemin);
                    }
                    catch (Exception ex)
                    {
                        Journal.Ecrire("Téléchargement OneDrive en échec : " + e.Chemin + " : " + ex);
                    }
                    if (OneDrive.EstEnLigneSeulement(e.Chemin))
                        throw new ErreurUtilisateur(L("OneDrive n'a pas pu télécharger {0}.", Nom(e)),
                                                    L("Vérifiez la connexion à Internet et que OneDrive est bien démarré, puis relancez. Sinon : clic droit sur le dossier, puis « Toujours conserver sur cet appareil »."));
                    e.Rafraichir();
                }

                if (Fichiers.EstVerrouille(e.Chemin, false))
                {
                    e.Etat = EtatPdf.Verrouille;
                    throw new ErreurUtilisateur(L("Le fichier {0} est verrouillé par un autre programme.", Nom(e)),
                                                L("Fermez le programme qui l'utilise, puis relancez la fusion."));
                }
                if (renommer && Fichiers.EstVerrouille(e.Chemin, true))
                {
                    e.Etat = EtatPdf.Verrouille;
                    throw new ErreurUtilisateur(L("Le fichier {0} est ouvert dans un autre programme.", Nom(e)),
                                                L("Il ne pourra pas être renommé. Fermez-le (par exemple dans votre lecteur PDF), ou décochez « Renommer », puis relancez."));
                }

                if (e.Pages < 0 || e.Etat != EtatPdf.Pret)
                {
                    InfoPages ip = qpdf.NombrePages(e.Chemin);
                    e.Pages = ip.Pages;
                    e.Etat = ip.Etat;
                    if (ip.Etat != EtatPdf.Pret) Journal.Ecrire("Contrôle en échec : " + e.Chemin + " : " + ip.Detail);
                }
                if (e.Etat == EtatPdf.Protege)
                    throw new ErreurUtilisateur(L("Le fichier {0} est protégé par un mot de passe.", Nom(e)),
                                                L("qpdf ne peut pas l'ouvrir. Retirez-le de la liste, ou ouvrez-le avec son mot de passe et enregistrez-en une copie sans protection (Imprimer > Microsoft Print to PDF), puis relancez."));
                if (e.Etat == EtatPdf.Endommage)
                    throw new ErreurUtilisateur(L("Le fichier {0} est endommagé ou n'est pas un vrai PDF.", Nom(e)),
                                                L("Ouvrez-le pour vérifier. Retirez-le de la liste, puis relancez la fusion."));
            }

            // --- fusion vers un fichier temporaire du même dossier ---------
            // Pas de demi-fichier sous le nom final : en cas d'échec ou de
            // coupure, rien ne laisse croire à un résultat.
            string dossier = Path.GetDirectoryName(sortie);
            string temp = Path.Combine(dossier, ".~FusionPDF-" + Fichiers.Court() + ".pdf");
            ResultatProcessus r;
            progres(30, L("Fusion en cours…"));
            string texteFusion = L("Fusion de {0} en cours…", Langue.Pl(n, "fichier", "fichiers"));
            try
            {
                r = qpdf.Fusionner(ordre.Select(x => x.Chemin).ToList(), temp, DelaiFusionMs, delegate(int pct)
                {
                    progres(30 + pct * 0.65, texteFusion);
                });
            }
            catch
            {
                Supprimer(temp);
                throw;
            }

            if (r.Code != 0 && r.Code != 3)
            {
                Supprimer(temp);
                Journal.Ecrire("qpdf a rendu " + r.Code + (r.DelaiDepasse ? " (délai dépassé)" : "") + " :" + Environment.NewLine + r.Erreurs);
                throw Traduire(r, ordre, temp);
            }
            if (r.Code == 3) Journal.Ecrire("qpdf : avertissements (code 3), résultat gardé :" + Environment.NewLine + r.Erreurs);

            if (!File.Exists(temp))
                throw new ErreurUtilisateur(L("qpdf n'a produit aucun fichier."), L("Le détail technique est dans le journal : {0}", Journal.Chemin));

            try
            {
                if (File.Exists(sortie)) File.Delete(sortie);
                File.Move(temp, sortie);
            }
            catch (IOException)
            {
                Supprimer(temp);
                throw new ErreurUtilisateur(L("Le fichier {0} est ouvert dans un autre programme.", L("« {0} »", Path.GetFileName(sortie))),
                                            L("Fermez-le (par exemple dans votre lecteur PDF), ou changez le nom du fichier de sortie, puis relancez."));
            }
            catch (UnauthorizedAccessException)
            {
                Supprimer(temp);
                throw new ErreurUtilisateur(L("Impossible d'enregistrer {0}.", L("« {0} »", Path.GetFileName(sortie))),
                                            L("Vous n'avez pas le droit d'écrire dans ce dossier. Choisissez un autre emplacement avec « Enregistrer sous… »."));
            }

            // --- conclusion sur le fichier relu, pas sur le code de sortie --
            progres(96, L("Relecture du résultat…"));
            InfoPages relu = qpdf.NombrePages(sortie);
            if (relu.Etat != EtatPdf.Pret)
            {
                Supprimer(sortie);
                Journal.Ecrire("Relecture du résultat en échec : " + relu.Detail);
                throw new ErreurUtilisateur(L("Le fichier produit n'a pas pu être relu : il a été supprimé."),
                                            L("Le détail technique est dans le journal : {0}", Journal.Chemin));
            }

            ResultatFusion res = new ResultatFusion();
            res.Sortie = sortie;
            res.Pages = relu.Pages;
            res.PagesAttendues = ordre.Sum(x => Math.Max(0, x.Pages));
            res.Fichiers = n;
            res.Avertissements = r.Code == 3;
            Journal.Ecrire("Fusion terminée : " + sortie + ", " + relu.Pages + " pages relues (" + res.PagesAttendues + " attendues), code qpdf " + r.Code);

            // --- renommage, seulement une fois la fusion réussie ------------
            if (renommer)
            {
                progres(98, L("Renommage des fichiers sources…"));
                try
                {
                    string dossierSources = ordre[0].Dossier;
                    List<LigneRenommage> plan = Renommage.Plan(ordre.Select(x => x.Nom).ToList());
                    res.Csv = Renommage.EcrireCsv(dossierSources, plan);
                    res.CheminsRenommes = Renommage.Appliquer(dossierSources, plan);
                    res.Renommes = res.CheminsRenommes.Count;
                    Journal.Ecrire("Renommage : " + res.Renommes + " fichier(s), journal " + res.Csv);
                }
                catch (ErreurUtilisateur eu)
                {
                    res.ErreurRenommage = eu;
                }
                catch (Exception ex)
                {
                    Journal.Ecrire("Renommage en échec : " + ex);
                    res.ErreurRenommage = new ErreurUtilisateur(L("Le renommage a échoué."), ex.Message);
                }
            }

            progres(100, L("Terminé"));
            return res;
        }

        static ErreurUtilisateur Traduire(ResultatProcessus r, IList<ElementPdf> ordre, string temp)
        {
            if (r.DelaiDepasse)
                return new ErreurUtilisateur(L("La fusion a été interrompue : elle durait depuis plus de 30 minutes."),
                                             L("Essayez avec moins de fichiers à la fois. Le détail technique est dans le journal : {0}", Journal.Chemin));

            // qpdf préfixe ses messages par le chemin du fichier en cause.
            foreach (string ligne in r.Erreurs.Split('\n'))
            {
                foreach (ElementPdf e in ordre)
                {
                    if (ligne.IndexOf(e.Chemin, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (ligne.IndexOf("invalid password", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        e.Etat = EtatPdf.Protege;
                        return new ErreurUtilisateur(L("Le fichier {0} est protégé par un mot de passe.", Nom(e)),
                                                     L("Retirez-le de la liste, ou enregistrez-en une copie sans protection, puis relancez."));
                    }
                    e.Etat = EtatPdf.Endommage;
                    return new ErreurUtilisateur(L("Le fichier {0} n'a pas pu être lu par qpdf.", Nom(e)),
                                                 L("Il est probablement endommagé. Retirez-le de la liste, puis relancez la fusion."));
                }
                if (ligne.IndexOf(temp, StringComparison.OrdinalIgnoreCase) >= 0)
                    return new ErreurUtilisateur(L("Impossible d'écrire le fichier fusionné dans ce dossier."),
                                                 L("Vérifiez l'espace disque et vos droits sur le dossier, ou choisissez un autre emplacement avec « Enregistrer sous… »."));
            }
            return new ErreurUtilisateur(L("qpdf a refusé la fusion (code {0}).", r.Code),
                                         L("Le détail technique est dans le journal : {0}", Journal.Chemin));
        }

        static void Supprimer(string chemin)
        {
            try { if (File.Exists(chemin)) File.Delete(chemin); } catch { }
        }
    }
}
