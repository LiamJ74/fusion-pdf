// Fusion PDF - la fenêtre : chargement du XAML, liste, glisser-déposer,
// aperçu, options, fusion en tâche de fond, bandeaux.
//
// Tout texte affiché passe par L("…") : voir Langue.cs.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace FusionPDF
{
    public sealed class Fenetre
    {
        const string FormatOrdre = "FusionPDF.Ordre";

        static string L(string fr) { return Langue.L(fr); }
        static string L(string fr, params object[] v) { return Langue.L(fr, v); }
        static string Pl(int n, string s, string p) { return Langue.Pl(n, s, p); }

        public Window W { get; private set; }

        readonly ObservableCollection<ElementPdf> elements = new ObservableCollection<ElementPdf>();
        readonly BlockingCollection<ElementPdf> aCompter = new BlockingCollection<ElementPdf>();
        Qpdf qpdf;
        bool occupe;

        // Fichier de sortie
        string dossierSortie;
        bool dossierChoisi;      // fixé par « Enregistrer sous… »
        string derniereSortie;

        // Glisser-déposer interne
        Point? departGlisser;
        ElementPdf elementPresse;
        bool differerSelection;
        List<ElementPdf> glisses;

        int jetonApercu;

        // Sous-dossiers : choix mémorisé, et dossiers chargés (pour que cocher
        // ou décocher agisse tout de suite sur la liste).
        bool sousDossiers;
        readonly List<string> racines = new List<string>();

        // Contrôles
        ListBox liste;
        TextBox txtSortie;
        CheckBox chkRenommer;
        Button btnFusionner, btnDossier, btnFichiers, btnMonter, btnDescendre, btnTriNom, btnTriDate,
               btnInverser, btnRetirer, btnEnregistrerSous, btnOuvrirPdf, btnOuvrirDossier, btnPortail, btnFermerBandeau;
        Hyperlink lienAnnuler;
        TextBlock txtResume, txtDossierSortie, txtQpdf, txtProgres, txtPourcent, txtApercuVide, txtApercuNom,
                  txtApercuInfo, icoApercu, bandeauIcone, bandeauTitre, bandeauTexte, txtAideFusion, txtZone,
                  txtNomVide, txtSuffixeSortie;
        Border bandeau, pastilleQpdf, cadrePage;
        Ellipse pointQpdf;
        Rectangle zoneCadre;
        FrameworkElement listeVide, apercuVide, zoneProgres, options, barreOutils, zoneDepot;
        ProgressBar progres;
        Image imgApercu;
        FrameworkElement aide, aidePanneau;

        public Fenetre()
        {
            // Le XAML est traduit (§…§) avant d'être chargé.
            string xaml;
            using (Stream s = Ressources.Flux("FusionPDF.MainWindow.xaml"))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                xaml = Langue.Xaml(r.ReadToEnd());
            W = (Window)XamlReader.Parse(xaml);

            liste = C<ListBox>("Liste");
            txtSortie = C<TextBox>("TxtSortie");
            txtNomVide = C<TextBlock>("TxtNomVide");
            txtSuffixeSortie = C<TextBlock>("TxtSuffixeSortie");
            chkRenommer = C<CheckBox>("ChkRenommer");
            btnFusionner = C<Button>("BtnFusionner");
            contenuFusionner = btnFusionner.Content;
            btnDossier = C<Button>("BtnDossier");
            btnFichiers = C<Button>("BtnFichiers");
            btnMonter = C<Button>("BtnMonter");
            btnDescendre = C<Button>("BtnDescendre");
            btnTriNom = C<Button>("BtnTriNom");
            btnTriDate = C<Button>("BtnTriDate");
            btnInverser = C<Button>("BtnInverser");
            btnRetirer = C<Button>("BtnRetirer");
            btnEnregistrerSous = C<Button>("BtnEnregistrerSous");
            btnOuvrirPdf = C<Button>("BtnOuvrirPdf");
            btnOuvrirDossier = C<Button>("BtnOuvrirDossier");
            btnPortail = C<Button>("BtnPortail");
            btnFermerBandeau = C<Button>("BtnFermerBandeau");
            lienAnnuler = C<Hyperlink>("LienAnnuler");
            txtResume = C<TextBlock>("TxtResume");
            txtDossierSortie = C<TextBlock>("TxtDossierSortie");
            txtQpdf = C<TextBlock>("TxtQpdf");
            txtProgres = C<TextBlock>("TxtProgres");
            txtPourcent = C<TextBlock>("TxtPourcent");
            txtApercuVide = C<TextBlock>("TxtApercuVide");
            txtApercuNom = C<TextBlock>("TxtApercuNom");
            txtApercuInfo = C<TextBlock>("TxtApercuInfo");
            icoApercu = C<TextBlock>("IcoApercu");
            bandeauIcone = C<TextBlock>("BandeauIcone");
            bandeauTitre = C<TextBlock>("BandeauTitre");
            bandeauTexte = C<TextBlock>("BandeauTexte");
            txtAideFusion = C<TextBlock>("TxtAideFusion");
            txtZone = C<TextBlock>("TxtZone");
            bandeau = C<Border>("Bandeau");
            pastilleQpdf = C<Border>("PastilleQpdf");
            cadrePage = C<Border>("CadrePage");
            pointQpdf = C<Ellipse>("PointQpdf");
            zoneCadre = C<Rectangle>("ZoneCadre");
            listeVide = C<FrameworkElement>("ListeVide");
            apercuVide = C<FrameworkElement>("ApercuVide");
            zoneProgres = C<FrameworkElement>("ZoneProgres");
            options = C<FrameworkElement>("Options");
            barreOutils = C<FrameworkElement>("BarreOutils");
            zoneDepot = C<FrameworkElement>("ZoneDepot");
            progres = C<ProgressBar>("Progres");
            imgApercu = C<Image>("ImgApercu");
            aide = C<FrameworkElement>("Aide");
            aidePanneau = C<FrameworkElement>("AidePanneau");

            // Numéro de version en bas à droite : 1.3.0, lu dans l'assembly (posé par le build).
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            TextBlock txtVersion = C<TextBlock>("TxtVersion");
            txtVersion.Text = "Fusion PDF " + v.ToString(3);
            txtVersion.ToolTip = v.ToString();
            C<Image>("ImgLogo").Source = Ressources.Image("FusionPDF.Logo.png");
            try { W.Icon = Ressources.Image("FusionPDF.Icone.ico"); } catch { }
            Ombrer();

            liste.ItemsSource = elements;
            elements.CollectionChanged += delegate { Renumeroter(); MajResume(); MajSortieParDefaut(); };

            Brancher();
            qpdf = Qpdf.Trouver();
            MajQpdf();
            MajResume();
            MajSortieParDefaut();
            AfficherApercu(null);

            Thread t = new Thread(BoucleComptage);
            t.IsBackground = true;
            t.Name = "Comptage des pages";
            t.Start();

            W.SourceInitialized += delegate { HabillerTitre(); };
            W.Loaded += delegate
            {
                if (qpdf == null) BandeauQpdfAbsent();
                // Premier lancement pour cet utilisateur : le mode d'emploi s'ouvre seul, une fois.
                if (!File.Exists(MarqueAideVue)) OuvrirAide();
            };
        }

        T C<T>(string nom) where T : class
        {
            T c = W.FindName(nom) as T;
            if (c == null) throw new InvalidOperationException("Contrôle introuvable dans MainWindow.xaml : " + nom);
            return c;
        }

        // =================================================================
        // Habillage moderne
        // =================================================================

        /// <summary>
        /// Windows 11 : la barre de titre prend le blanc de l'en-tête et une
        /// bordure gris clair, pour une fenêtre d'un seul tenant. Sans effet
        /// (et sans erreur) sur Windows 10.
        /// </summary>
        void HabillerTitre()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(W).Handle;
                int blanc = 0x00FFFFFF;            // COLORREF : 0x00BBGGRR
                int encre = 0x00271811;            // #111827
                int bord = 0x00EBE7E5;             // #E5E7EB
                int coins = 2;                     // DWMWCP_ROUND
                DwmSetWindowAttribute(h, 35, ref blanc, 4);   // DWMWA_CAPTION_COLOR
                DwmSetWindowAttribute(h, 36, ref encre, 4);   // DWMWA_TEXT_COLOR
                DwmSetWindowAttribute(h, 34, ref bord, 4);    // DWMWA_BORDER_COLOR
                DwmSetWindowAttribute(h, 33, ref coins, 4);   // DWMWA_WINDOW_CORNER_PREFERENCE
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valeur, int taille);

        /// <summary>Ombre douce (shadow-sm de Tailwind) sous les cartes blanches.</summary>
        void Ombrer()
        {
            Ombre(C<Border>("CarteListe"));
            Ombre(C<Border>("CarteApercu"));
        }

        static void Ombre(Border b)
        {
            if (b == null) return;
            DropShadowEffect o = new DropShadowEffect();
            o.BlurRadius = 10;
            o.ShadowDepth = 1;
            o.Direction = 270;
            o.Opacity = 0.07;
            o.Color = Colors.Black;
            b.Effect = o;
        }

        // =================================================================
        // Aide intégrée
        // =================================================================

        /// <summary>FAQ complète, déposée par install.ps1 à côté de l'exe.</summary>
        static string CheminFaq
        {
            get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FAQ Fusion PDF.pdf"); }
        }

        static string MarqueAideVue
        {
            get { return Path.Combine(Path.GetDirectoryName(Journal.Chemin), "aide-vue.txt"); }
        }

        void OuvrirAide()
        {
            if (aide.Visibility == Visibility.Visible) return;
            aide.Visibility = Visibility.Visible;
            TranslateTransform t = new TranslateTransform(40, 0);
            aidePanneau.RenderTransform = t;
            DoubleAnimation glisse = new DoubleAnimation(40, 0, new Duration(TimeSpan.FromMilliseconds(220)));
            glisse.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            t.BeginAnimation(TranslateTransform.XProperty, glisse);
            aide.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180))));
            C<Button>("BtnCompris").Focus();
        }

        void FermerAide()
        {
            aide.Visibility = Visibility.Collapsed;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MarqueAideVue));
                File.WriteAllText(MarqueAideVue, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { }
        }

        // =================================================================
        // Sous-dossiers
        // =================================================================

        /// <summary>Le dossier chargé qui contient ce dossier (lui-même ou un parent), ou null.</summary>
        string RacineDe(string dossier)
        {
            return racines.FirstOrDefault(r => string.Equals(dossier.TrimEnd('\\'), r, StringComparison.OrdinalIgnoreCase)
                                                || Fichiers.EstSousDossierDe(dossier, r));
        }

        static string LirePreference(string nom)
        {
            try
            {
                string f = Path.Combine(Path.GetDirectoryName(Journal.Chemin), nom);
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
            catch { return ""; }
        }

        static void EcrirePreference(string nom, string valeur)
        {
            try
            {
                string f = Path.Combine(Path.GetDirectoryName(Journal.Chemin), nom);
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                File.WriteAllText(f, valeur);
            }
            catch { }
        }

        /// <summary>
        /// Cocher ajoute les PDF des sous-dossiers des dossiers déjà chargés ;
        /// décocher les retire. Les fichiers ajoutés un par un ne bougent pas.
        /// </summary>
        void BasculerSousDossiers(bool oui)
        {
            if (caseParProgramme || oui == sousDossiers) return;
            sousDossiers = oui;
            EcrirePreference("sous-dossiers.txt", oui ? "1" : "0");
            if (occupe || racines.Count == 0) return;
            MasquerBandeau();

            if (oui) InsererSousDossiers();
            else
            {
                List<ElementPdf> retires = elements.Where(e => racines.Any(r => Fichiers.EstSousDossierDe(e.Dossier, r))).ToList();
                foreach (ElementPdf e in retires) elements.Remove(e);
                if (retires.Count > 0)
                    Information(L("{0} de la liste.", Pl(retires.Count, "PDF de sous-dossier retiré", "PDF de sous-dossiers retirés")), "");
            }
        }

        void InsererSousDossiers()
        {
            {
                // Chaque dossier chargé est RECONSTRUIT dans l'ordre de
                // l'Explorateur, à la place de son bloc dans la liste : ses
                // sous-dossiers d'abord, puis ses PDF. Les fichiers ajoutés à part,
                // hors de ces dossiers, gardent leur place.
                Dictionary<string, ElementPdf> existants = elements.ToDictionary(e => e.Chemin, StringComparer.OrdinalIgnoreCase);
                List<ElementPdf> nouvelle = new List<ElementPdf>();
                HashSet<string> faits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> places = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<ElementPdf> ajoutes = new List<ElementPdf>();
                Action<string> poserArbre = delegate(string r)
                {
                    if (!faits.Add(r)) return;
                    foreach (string f in Fichiers.PdfDuDossier(r, true))
                    {
                        string c = Path.GetFullPath(f);
                        if (!places.Add(c)) continue;
                        ElementPdf e;
                        if (!existants.TryGetValue(c, out e)) { e = new ElementPdf(c); ajoutes.Add(e); }
                        nouvelle.Add(e);
                    }
                };
                foreach (ElementPdf e in elements)
                {
                    string r = RacineDe(e.Dossier);
                    if (r != null) poserArbre(r);
                    else if (places.Add(e.Chemin)) nouvelle.Add(e);
                }
                foreach (string r in racines) poserArbre(r);   // dossiers sans PDF à leur niveau

                List<ElementPdf> sel = Selection();
                elements.Clear();
                foreach (ElementPdf e in nouvelle) elements.Add(e);
                foreach (ElementPdf e in ajoutes) if (e.Etat == EtatPdf.Lecture) aCompter.Add(e);
                Reselectionner(sel.Where(e => elements.Contains(e)).ToList());
                if (ajoutes.Count > 0)
                {
                    liste.ScrollIntoView(ajoutes[0]);
                    Information(L("{0} depuis les sous-dossiers.", Pl(ajoutes.Count, "PDF ajouté", "PDF ajoutés")),
                                L("La liste suit l'ordre de l'Explorateur : à chaque niveau, les sous-dossiers d'abord, puis les PDF du dossier."));
                }
                else Information(L("Aucun PDF dans les sous-dossiers."), "");
            }
        }

        /// <summary>
        /// Pour l'autotest : charge les chemins case décochée, puis coche la case
        /// comme l'utilisateur, sans toucher à sa préférence mémorisée. Rend
        /// l'ordre de la liste.
        /// </summary>
        internal List<string> EssaiCocherApresChargement(IEnumerable<string> chemins)
        {
            sousDossiers = false;
            Ajouter(chemins, -1);
            sousDossiers = true;
            InsererSousDossiers();
            return elements.Select(e => e.Chemin).ToList();
        }

        // =================================================================
        // Langue : sélecteur FR | EN de l'en-tête
        // =================================================================

        void MajLangue()
        {
            Button fr = C<Button>("BtnFr"), en = C<Button>("BtnEn");
            Button actif = Langue.Anglais ? en : fr;
            Button autre = Langue.Anglais ? fr : en;
            actif.Background = Couleurs.P50;
            actif.Foreground = Couleurs.P700;
            autre.ClearValue(Control.BackgroundProperty);
            autre.ClearValue(Control.ForegroundProperty);
        }

        /// <summary>
        /// Le XAML est traduit au chargement : changer de langue relance donc la
        /// fenêtre. Le choix est mémorisé, et la liste en cours repasse, dans son
        /// ordre, par un fichier temporaire (pas de limite de ligne de commande).
        /// </summary>
        void ChangerLangue(bool anglais)
        {
            if (anglais == Langue.Anglais) return;
            if (occupe)
            {
                Information(L("Attendez la fin de la fusion pour changer de langue."), "");
                return;
            }
            Langue.Memoriser(anglais);
            string arguments = "";
            if (elements.Count > 0)
            {
                string liste = Path.Combine(Path.GetTempPath(), "FusionPDF-liste-" + Fichiers.Court() + ".txt");
                File.WriteAllLines(liste, elements.Select(e => e.Chemin), new UTF8Encoding(false));
                arguments = "--liste \"" + liste + "\"";
            }
            Journal.Ecrire("Changement de langue : " + (anglais ? "en" : "fr") + ", relance avec " + elements.Count + " fichier(s)");
            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, arguments) { UseShellExecute = false });
            W.Close();
        }

        // =================================================================
        // Événements
        // =================================================================

        void Brancher()
        {
            btnDossier.Click += delegate { ChoisirDossier(); };
            btnFichiers.Click += delegate { ChoisirFichiers(); };
            btnMonter.Click += delegate { Deplacer(-1); };
            btnDescendre.Click += delegate { Deplacer(1); };
            // Ordre de l'arborescence : identique au tri naturel des noms dans un
            // seul dossier, et juste avec des sous-dossiers (dossier par dossier).
            btnTriNom.Click += delegate { Reordonner(elements.OrderBy(e => e.Chemin, TriArbre.Instance)); };
            btnTriDate.Click += delegate { Reordonner(elements.OrderBy(e => e.Date).ThenBy(e => e.Nom, TriNaturel.Instance)); };
            btnInverser.Click += delegate { Reordonner(elements.Reverse()); };
            btnRetirer.Click += delegate { Retirer(); };
            btnFusionner.Click += delegate { Fusionner(); };
            btnEnregistrerSous.Click += delegate { EnregistrerSous(); };
            btnOuvrirPdf.Click += delegate { Ouvrir(derniereSortie, false); };
            btnOuvrirDossier.Click += delegate { Ouvrir(derniereSortie, true); };
            btnPortail.Click += delegate { OuvrirPortail(); };
            btnFermerBandeau.Click += delegate { MasquerBandeau(); };
            lienAnnuler.Click += delegate { AnnulerRenommage(); };
            CheckBox chkSous = C<CheckBox>("ChkSousDossiers");
            sousDossiers = LirePreference("sous-dossiers.txt") == "1";
            chkSous.IsChecked = sousDossiers;
            chkSous.Checked += delegate { BasculerSousDossiers(true); };
            chkSous.Unchecked += delegate { BasculerSousDossiers(false); };
            C<Button>("BtnFr").Click += delegate { ChangerLangue(false); };
            C<Button>("BtnEn").Click += delegate { ChangerLangue(true); };
            MajLangue();
            C<Button>("BtnAide").Click += delegate { OuvrirAide(); };
            C<Button>("BtnFermerAide").Click += delegate { FermerAide(); };
            C<Button>("BtnCompris").Click += delegate { FermerAide(); };
            C<Border>("AideFond").MouseLeftButtonDown += delegate { FermerAide(); };
            Button btnFaq = C<Button>("BtnFaqPdf");
            btnFaq.Visibility = File.Exists(CheminFaq) ? Visibility.Visible : Visibility.Collapsed;
            btnFaq.Click += delegate { Ouvrir(CheminFaq, false); };

            txtSortie.TextChanged += delegate { MajNomSortie(); MajResume(); };
            MajNomSortie();
            liste.SelectionChanged += delegate { MajBoutons(); AfficherApercu(liste.SelectedItem as ElementPdf); };

            liste.PreviewMouseLeftButtonDown += Liste_Presse;
            liste.PreviewMouseLeftButtonUp += Liste_Relache;
            liste.PreviewMouseMove += Liste_Bouge;
            liste.DragOver += Liste_DragOver;
            liste.DragLeave += delegate { EffacerMarques(); };
            liste.Drop += Liste_Drop;
            liste.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Delete && !occupe) { Retirer(); e.Handled = true; }
            };

            W.DragEnter += Fenetre_DragOver;
            W.DragOver += Fenetre_DragOver;
            W.DragLeave += delegate { Surligner(false); };
            W.Drop += Fenetre_Drop;
            W.PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.F1) { OuvrirAide(); e.Handled = true; return; }
                if (e.Key == Key.Escape && aide.Visibility == Visibility.Visible) { FermerAide(); e.Handled = true; return; }
                if (occupe || Keyboard.Modifiers != ModifierKeys.Alt) return;
                if (e.SystemKey == Key.Up) { Deplacer(-1); e.Handled = true; }
                else if (e.SystemKey == Key.Down) { Deplacer(1); e.Handled = true; }
            };
            W.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
            {
                if (occupe && MessageBox.Show(W, L("Une fusion est en cours. Fermer quand même ?"), "Fusion PDF",
                                              MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    e.Cancel = true;
            };
            MajBoutons();
        }

        // =================================================================
        // Ajout de fichiers
        // =================================================================

        /// <summary>
        /// Point d'entrée du clic droit et de la ligne de commande.
        /// avecSousDossiers : second clic droit « … et de ses sous-dossiers »
        /// (--sous-dossiers). Vaut pour CE chargement : la case s'affiche cochée,
        /// mais la préférence mémorisée de l'utilisateur ne change pas.
        /// </summary>
        public void Charger(IEnumerable<string> chemins, bool avecSousDossiers)
        {
            List<string> l = chemins.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (avecSousDossiers && !sousDossiers)
            {
                sousDossiers = true;
                caseParProgramme = true;
                C<CheckBox>("ChkSousDossiers").IsChecked = true;
                caseParProgramme = false;
            }
            if (l.Count > 0) Ajouter(l, -1);
        }

        bool caseParProgramme;

        void ChoisirDossier()
        {
            string initial = dossierSortie;
            if (string.IsNullOrEmpty(initial)) initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string d;
            try
            {
                d = Dialogues.ChoisirDossier(new WindowInteropHelper(W).Handle, L("Choisir le dossier qui contient les PDF"), initial);
            }
            catch (Exception e)
            {
                Journal.Ecrire("Sélecteur de dossier : " + e);
                Microsoft.Win32.OpenFileDialog repli = new Microsoft.Win32.OpenFileDialog();
                repli.Title = L("Choisissez un PDF du dossier");
                repli.Filter = "PDF (*.pdf)|*.pdf";
                if (repli.ShowDialog(W) != true) return;
                d = Path.GetDirectoryName(repli.FileName);
            }
            if (d != null) Ajouter(new[] { d }, -1);
        }

        void ChoisirFichiers()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = L("Ajouter des PDF");
            dlg.Filter = "PDF (*.pdf)|*.pdf";
            dlg.Multiselect = true;
            if (!string.IsNullOrEmpty(dossierSortie) && Directory.Exists(dossierSortie)) dlg.InitialDirectory = dossierSortie;
            if (dlg.ShowDialog(W) == true)
                Ajouter(dlg.FileNames.OrderBy(f => Path.GetFileName(f), TriNaturel.Instance), -1);
        }

        static bool EstPdf(string chemin)
        {
            return string.Equals(Path.GetExtension(chemin), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ajoute des fichiers et des dossiers (PDF du premier niveau, en tri
        /// naturel, sans les fusions déjà produites). index &lt; 0 : à la fin.
        /// </summary>
        void Ajouter(IEnumerable<string> chemins, int index)
        {
            if (occupe) return;
            MasquerBandeau();
            List<string> nouveaux = new List<string>();
            List<string> dossiersVides = new List<string>();
            int ignores = 0;

            foreach (string brut in chemins)
            {
                string c = brut.Trim().Trim('"');
                try
                {
                    if (Directory.Exists(c))
                    {
                        List<string> pdf = Fichiers.PdfDuDossier(c, sousDossiers);
                        string racine = Path.GetFullPath(c).TrimEnd('\\');
                        if (!racines.Contains(racine, StringComparer.OrdinalIgnoreCase)) racines.Add(racine);
                        if (pdf.Count == 0) dossiersVides.Add(c);
                        if (string.IsNullOrEmpty(dossierSortie) && !dossierChoisi && elements.Count == 0 && nouveaux.Count == 0)
                            dossierSortie = c;
                        nouveaux.AddRange(pdf);
                    }
                    else if (File.Exists(c) && EstPdf(c)) nouveaux.Add(c);
                    else ignores++;
                }
                catch (Exception e)
                {
                    Journal.Ecrire("Ajout impossible : " + c + " : " + e.Message);
                    ignores++;
                }
            }

            HashSet<string> deja = new HashSet<string>(elements.Select(e => e.Chemin), StringComparer.OrdinalIgnoreCase);
            List<ElementPdf> ajoutes = new List<ElementPdf>();
            foreach (string f in nouveaux)
            {
                string complet = Path.GetFullPath(f);
                if (!deja.Add(complet)) continue;
                ajoutes.Add(new ElementPdf(complet));
            }

            int pos = index < 0 || index > elements.Count ? elements.Count : index;
            foreach (ElementPdf e in ajoutes) elements.Insert(pos++, e);
            foreach (ElementPdf e in ajoutes) if (e.Etat == EtatPdf.Lecture) aCompter.Add(e);

            if (ajoutes.Count > 0)
            {
                liste.SelectedItems.Clear();
                liste.SelectedItem = ajoutes[0];
                liste.ScrollIntoView(ajoutes[0]);
            }

            if (ajoutes.Count == 0 && dossiersVides.Count > 0)
            {
                // Rien au premier niveau : y en a-t-il plus bas ? On le dit,
                // plutôt que de laisser croire que le dossier est vide.
                int plusBas = sousDossiers ? 0 : Fichiers.PdfDuDossier(dossiersVides[0], true).Count;
                if (plusBas > 0)
                    Information(L("Le dossier {0} ne contient pas de PDF directement, mais {1} dans ses sous-dossiers.", Q(NomDossier(dossiersVides[0])), plusBas),
                                L("Cochez « Inclure les sous-dossiers » pour les ajouter."));
                else
                    Erreur(L("Le dossier {0} ne contient aucun PDF.", Q(NomDossier(dossiersVides[0]))),
                           L("Ni directement, ni dans ses sous-dossiers. Dossier : {0}", dossiersVides[0]));
            }
            else if (ignores > 0)
            {
                Information(L("{0} : ce ne sont pas des PDF.", Pl(ignores, "élément ignoré", "éléments ignorés")),
                            ajoutes.Count > 0 ? L("{0} à la liste.", Pl(ajoutes.Count, "PDF ajouté", "PDF ajoutés")) : L("Aucun PDF n'a été ajouté."));
            }
        }

        static string NomDossier(string d)
        {
            string n = Path.GetFileName(d.TrimEnd('\\'));
            return string.IsNullOrEmpty(n) ? d : n;
        }

        /// <summary>Un nom entre guillemets de la langue : « x » ou “x”.</summary>
        static string Q(string x)
        {
            return L("« {0} »", x);
        }

        /// <summary>Compte les pages en tâche de fond, un fichier après l'autre.</summary>
        void BoucleComptage()
        {
            foreach (ElementPdf e in aCompter.GetConsumingEnumerable())
            {
                try
                {
                    if (!elements.Contains(e) || e.Etat != EtatPdf.Lecture) continue;
                    Qpdf q = qpdf;
                    if (q == null) continue;
                    InfoPages ip = q.NombrePages(e.Chemin);
                    e.Pages = ip.Pages;
                    e.Etat = ip.Etat;
                    W.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        MajResume();
                        if (liste.SelectedItem == e) AfficherApercu(e);
                    }));
                }
                catch (Exception ex)
                {
                    Journal.Ecrire("Comptage des pages : " + e.Chemin + " : " + ex.Message);
                }
            }
        }

        // =================================================================
        // Ordre de la liste
        // =================================================================

        List<ElementPdf> Selection()
        {
            return liste.SelectedItems.Cast<ElementPdf>().OrderBy(e => elements.IndexOf(e)).ToList();
        }

        void Reselectionner(IList<ElementPdf> sel)
        {
            liste.SelectedItems.Clear();
            foreach (ElementPdf e in sel) liste.SelectedItems.Add(e);
            if (sel.Count > 0) liste.ScrollIntoView(sel[0]);
        }

        void Deplacer(int pas)
        {
            if (occupe) return;
            List<ElementPdf> sel = Selection();
            if (sel.Count == 0) return;
            if (pas < 0 && elements.IndexOf(sel[0]) == 0) return;
            if (pas > 0 && elements.IndexOf(sel[sel.Count - 1]) == elements.Count - 1) return;
            IEnumerable<ElementPdf> ordre = pas > 0 ? sel.AsEnumerable().Reverse() : sel;
            foreach (ElementPdf e in ordre)
            {
                int i = elements.IndexOf(e);
                elements.Move(i, i + pas);
            }
            Reselectionner(sel);
        }

        void Reordonner(IEnumerable<ElementPdf> nouvelOrdre)
        {
            if (occupe) return;
            List<ElementPdf> sel = Selection();
            List<ElementPdf> l = nouvelOrdre.ToList();
            for (int i = 0; i < l.Count; i++)
            {
                int j = elements.IndexOf(l[i]);
                if (j != i) elements.Move(j, i);
            }
            Reselectionner(sel);
        }

        void Retirer()
        {
            if (occupe) return;
            List<ElementPdf> sel = Selection();
            if (sel.Count == 0) return;
            int premier = elements.IndexOf(sel[0]);
            foreach (ElementPdf e in sel) elements.Remove(e);
            if (elements.Count > 0) liste.SelectedItem = elements[Math.Min(premier, elements.Count - 1)];
        }

        void Renumeroter()
        {
            for (int i = 0; i < elements.Count; i++) elements[i].Rang = i + 1;
            listeVide.Visibility = elements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MajBoutons();
        }

        void MajBoutons()
        {
            bool sel = liste.SelectedItems.Count > 0;
            bool plusieurs = elements.Count > 1;
            btnMonter.IsEnabled = !occupe && sel;
            btnDescendre.IsEnabled = !occupe && sel;
            btnRetirer.IsEnabled = !occupe && sel;
            btnTriNom.IsEnabled = !occupe && plusieurs;
            btnTriDate.IsEnabled = !occupe && plusieurs;
            btnInverser.IsEnabled = !occupe && plusieurs;
            btnFusionner.IsEnabled = !occupe && elements.Count > 0;
        }

        void MajResume()
        {
            if (elements.Count == 0)
            {
                txtResume.Text = L("Liste vide");
                txtAideFusion.Text = L("Ajoutez au moins deux PDF");
                return;
            }
            int pages = elements.Where(e => e.Pages >= 0).Sum(e => e.Pages);
            int inconnus = elements.Count(e => e.Pages < 0 && (e.Etat == EtatPdf.Lecture || e.Etat == EtatPdf.EnLigne));
            long taille = elements.Sum(e => e.Taille);
            string t = Pl(elements.Count, "fichier", "fichiers") + "  ·  " + Format.Pages(pages);
            if (inconnus > 0) t += " " + L("(+ {0} à lire)", inconnus);
            txtResume.Text = t + "  ·  " + Format.Taille(taille);
            int enLigne = elements.Count(e => e.Etat == EtatPdf.EnLigne);
            txtAideFusion.Text = enLigne > 0
                ? L("{0} d'abord", Pl(enLigne, "fichier sera téléchargé", "fichiers seront téléchargés"))
                : elements.Count < 2 ? L("Ajoutez au moins deux PDF")
                : Fichiers.NomSaisi(txtSortie.Text).Length == 0 ? L("Donnez d'abord un nom au fichier")
                : L("{0} dans l'ordre affiché", Pl(elements.Count, "fichier", "fichiers"));
        }

        // =================================================================
        // Glisser-déposer : réordonner, et déposer depuis l'Explorateur
        // =================================================================

        static T Ancetre<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null && !(d is T))
                d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            return d as T;
        }

        static T Descendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(d, i);
                T t = c as T ?? Descendant<T>(c);
                if (t != null) return t;
            }
            return null;
        }

        void Liste_Presse(object sender, MouseButtonEventArgs e)
        {
            departGlisser = null;
            differerSelection = false;
            if (occupe || Ancetre<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject) != null) return;
            ListBoxItem lbi = Ancetre<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (lbi == null) return;
            departGlisser = e.GetPosition(liste);
            elementPresse = lbi.DataContext as ElementPdf;
            // Un clic sur une ligne déjà sélectionnée parmi plusieurs ne doit pas
            // réduire la sélection tout de suite : c'est peut-être le début d'un
            // glisser de toutes les lignes choisies.
            if (lbi.IsSelected && Keyboard.Modifiers == ModifierKeys.None && liste.SelectedItems.Count > 1)
            {
                differerSelection = true;
                e.Handled = true;
                liste.Focus();
            }
        }

        void Liste_Relache(object sender, MouseButtonEventArgs e)
        {
            if (differerSelection && elementPresse != null)
            {
                liste.SelectedItems.Clear();
                liste.SelectedItem = elementPresse;
            }
            differerSelection = false;
            departGlisser = null;
        }

        void Liste_Bouge(object sender, MouseEventArgs e)
        {
            if (departGlisser == null || e.LeftButton != MouseButtonState.Pressed || occupe || elementPresse == null) return;
            Vector d = e.GetPosition(liste) - departGlisser.Value;
            if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            if (!liste.SelectedItems.Contains(elementPresse))
            {
                liste.SelectedItems.Clear();
                liste.SelectedItem = elementPresse;
            }
            differerSelection = false;
            departGlisser = null;
            glisses = Selection();
            try
            {
                DragDrop.DoDragDrop(liste, new DataObject(FormatOrdre, "1"), DragDropEffects.Move);
            }
            finally
            {
                glisses = null;
                EffacerMarques();
            }
        }

        /// <summary>Position d'insertion sous le pointeur, de 0 à elements.Count.</summary>
        int IndexInsertion(DragEventArgs e)
        {
            ListBoxItem lbi = Ancetre<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (lbi == null) return elements.Count;
            ElementPdf cible = lbi.DataContext as ElementPdf;
            int i = elements.IndexOf(cible);
            if (i < 0) return elements.Count;
            return e.GetPosition(lbi).Y > lbi.ActualHeight / 2 ? i + 1 : i;
        }

        void MarquerInsertion(int index)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].LigneAvant = i == index;
                elements[i].LigneApres = index == elements.Count && i == elements.Count - 1;
            }
        }

        void EffacerMarques()
        {
            foreach (ElementPdf e in elements) { e.LigneAvant = false; e.LigneApres = false; }
        }

        void DefilerPendantGlisser(DragEventArgs e)
        {
            ScrollViewer sv = Descendant<ScrollViewer>(liste);
            if (sv == null) return;
            double y = e.GetPosition(liste).Y;
            if (y < 28) sv.ScrollToVerticalOffset(sv.VerticalOffset - 12);
            else if (y > liste.ActualHeight - 28) sv.ScrollToVerticalOffset(sv.VerticalOffset + 12);
        }

        void Liste_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (occupe) { e.Effects = DragDropEffects.None; return; }
            bool interne = e.Data.GetDataPresent(FormatOrdre) && glisses != null;
            bool externe = e.Data.GetDataPresent(DataFormats.FileDrop);
            if (!interne && !externe) { e.Effects = DragDropEffects.None; return; }
            e.Effects = interne ? DragDropEffects.Move : DragDropEffects.Copy;
            MarquerInsertion(IndexInsertion(e));
            DefilerPendantGlisser(e);
            if (externe) Surligner(true);
        }

        void Liste_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            int index = IndexInsertion(e);
            EffacerMarques();
            Surligner(false);
            if (occupe) return;

            if (e.Data.GetDataPresent(FormatOrdre) && glisses != null)
            {
                List<ElementPdf> deplaces = glisses;
                int avant = deplaces.Count(x => elements.IndexOf(x) < index);
                foreach (ElementPdf x in deplaces) elements.Remove(x);
                index = Math.Max(0, Math.Min(elements.Count, index - avant));
                for (int k = 0; k < deplaces.Count; k++) elements.Insert(index + k, deplaces[k]);
                Reselectionner(deplaces);
                return;
            }
            string[] fichiers = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (fichiers != null) Ajouter(OrdreDepose(fichiers), elements.Count == 0 ? -1 : index);
        }

        void Fenetre_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (!occupe && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Surligner(true);
            }
            else e.Effects = DragDropEffects.None;
        }

        void Fenetre_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            Surligner(false);
            string[] fichiers = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (fichiers != null && !occupe) Ajouter(OrdreDepose(fichiers), -1);
        }

        /// <summary>L'Explorateur livre une sélection dans un ordre arbitraire : on la remet en tri naturel.</summary>
        static IEnumerable<string> OrdreDepose(string[] fichiers)
        {
            return fichiers.OrderBy(f => Directory.Exists(f) ? 0 : 1).ThenBy(f => Path.GetFileName(f), TriNaturel.Instance);
        }

        void Surligner(bool oui)
        {
            zoneCadre.Stroke = oui ? Couleurs.P500 : Couleurs.P300;
            zoneCadre.StrokeThickness = oui ? 2 : 1.5;
            zoneCadre.Fill = oui ? Couleurs.P100 : new SolidColorBrush(Color.FromRgb(0xF5, 0xF9, 0xFF));
            txtZone.Text = oui ? L("Déposez pour ajouter à la liste") : L("Glissez ici des PDF ou un dossier");
        }

        // =================================================================
        // Aperçu
        // =================================================================

        void AfficherApercu(ElementPdf e)
        {
            int jeton = ++jetonApercu;
            if (e == null)
            {
                ApercuMessage("", L("Sélectionnez un fichier pour voir sa première page."));
                txtApercuNom.Text = "";
                txtApercuInfo.Text = "";
                return;
            }
            txtApercuNom.Text = e.Nom;
            string info = (e.Pages >= 0 ? Format.Pages(e.Pages) + "  ·  " : "") + Format.Taille(e.Taille)
                        + "  ·  " + Format.Date(e.Date) + "\n" + e.Dossier;
            txtApercuInfo.Text = info;

            if (e.Etat == EtatPdf.EnLigne) { ApercuMessage("", L("Fichier en ligne seulement : il sera téléchargé depuis OneDrive au moment de la fusion. L'aperçu s'affichera ensuite.")); return; }
            if (e.Etat == EtatPdf.Protege) { ApercuMessage("", L("Fichier protégé par un mot de passe : il ne pourra pas être fusionné.")); return; }
            if (e.Etat == EtatPdf.Endommage) { ApercuMessage("", L("Fichier illisible : il est endommagé ou ce n'est pas un vrai PDF.")); return; }
            if (e.Taille > Apercu.TailleMax) { ApercuMessage("", L("Fichier volumineux : pas d'aperçu, la fusion n'est pas concernée.")); return; }

            if (e.Apercu != null) { ApercuImage(e.Apercu); return; }
            ApercuMessage("", L("Préparation de l'aperçu…"));
            string chemin = e.Chemin;
            Task.Run(delegate
            {
                byte[] png = Apercu.PremierePage(chemin, 560);
                W.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (png != null && e.Chemin == chemin) e.Apercu = png;
                    if (jeton != jetonApercu) return;
                    if (png != null) ApercuImage(png);
                    else ApercuMessage("", L("Aperçu indisponible pour ce fichier. La fusion n'est pas concernée."));
                }));
            });
        }

        void ApercuMessage(string icone, string texte)
        {
            cadrePage.Visibility = Visibility.Collapsed;
            apercuVide.Visibility = Visibility.Visible;
            icoApercu.Text = icone;
            txtApercuVide.Text = texte;
        }

        void ApercuImage(byte[] png)
        {
            BitmapImage bi = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(png))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
            }
            bi.Freeze();
            imgApercu.Source = bi;
            apercuVide.Visibility = Visibility.Collapsed;
            cadrePage.Visibility = Visibility.Visible;
            Apparaitre(cadrePage);
        }

        // =================================================================
        // Fichier de sortie
        // =================================================================

        void MajSortieParDefaut()
        {
            // Le dossier que l'utilisateur a ouvert, pas le sous-dossier du premier
            // PDF : avec « Inclure les sous-dossiers », le premier fichier peut
            // venir d'un sous-dossier.
            if (!dossierChoisi && elements.Count > 0)
            {
                string premier = elements[0].Dossier;
                string racine = racines.FirstOrDefault(r => string.Equals(premier.TrimEnd('\\'), r, StringComparison.OrdinalIgnoreCase)
                                                            || Fichiers.EstSousDossierDe(premier, r));
                dossierSortie = racine ?? premier;
            }
            txtDossierSortie.Text = string.IsNullOrEmpty(dossierSortie)
                ? L("Enregistré dans le dossier des fichiers sources.")
                : L("Enregistré dans : {0}", dossierSortie);
            MajNomSortie();
        }

        /// <summary>
        /// La case ne contient que la partie choisie par l'utilisateur ; la fin
        /// « _FUSION_&lt;date&gt;.pdf » est fixe et affichée à côté. Case vide :
        /// texte d'invite, et Fusionner refuse (demandé le 21/10 : les gens ne
        /// mettaient rien devant le _).
        /// </summary>
        void MajNomSortie()
        {
            bool vide = Fichiers.NomSaisi(txtSortie.Text).Length == 0;
            txtNomVide.Visibility = string.IsNullOrEmpty(txtSortie.Text) ? Visibility.Visible : Visibility.Collapsed;
            txtSuffixeSortie.Text = Fichiers.PrefixeSortie + Fichiers.Horodatage() + ".pdf";
            if (!vide && erreurNom)
            {
                // Le nom est tapé : le message rouge n'a plus lieu d'être.
                erreurNom = false;
                txtSortie.ClearValue(Control.BorderBrushProperty);
                MasquerBandeau();
            }
        }

        bool erreurNom;

        void SignalerNomManquant()
        {
            erreurNom = true;
            txtSortie.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            txtSortie.Focus();
        }

        void EnregistrerSous()
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = L("Enregistrer le PDF fusionné");
            dlg.Filter = "PDF (*.pdf)|*.pdf";
            dlg.DefaultExt = ".pdf";
            dlg.AddExtension = true;
            dlg.OverwritePrompt = false;   // le nom final porte la date, NomLibre évite l'écrasement
            string saisi = Fichiers.NomSaisi(txtSortie.Text);
            dlg.FileName = saisi.Length > 0 ? saisi : L("Nom du document");
            if (!string.IsNullOrEmpty(dossierSortie) && Directory.Exists(dossierSortie)) dlg.InitialDirectory = dossierSortie;
            if (dlg.ShowDialog(W) != true) return;
            dossierSortie = Path.GetDirectoryName(dlg.FileName);
            dossierChoisi = true;
            // Le nom choisi ici devient la partie saisie ; la marque _FUSION_<date> s'ajoute toujours.
            txtSortie.Text = Fichiers.NomSaisi(Path.GetFileName(dlg.FileName));
            MajSortieParDefaut();
        }

        string CheminSortie()
        {
            string nom = Fichiers.NomSaisi(txtSortie.Text);
            if (nom.Length == 0)
            {
                SignalerNomManquant();
                throw new ErreurUtilisateur(L("Donnez un nom au fichier fusionné."),
                                            L("Tapez-le dans la case « Fichier de sortie », devant {0} : par exemple « Dossier Dupont ».", Fichiers.PrefixeSortie + "…"));
            }
            if (nom.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                txtSortie.Focus();
                throw new ErreurUtilisateur(L("Le nom du fichier de sortie n'est pas valide."), L("Ces caractères ne sont pas permis : {0}", "\\ / : * ? \" < > |"));
            }
            string d = dossierSortie;
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d))
                throw new ErreurUtilisateur(L("Le dossier de destination n'existe plus."), L("Choisissez un emplacement avec « Enregistrer sous… »."));
            return Path.Combine(d, Fichiers.NomLibre(d, Fichiers.NomSortie(nom, Fichiers.Horodatage())));
        }

        // =================================================================
        // Fusion
        // =================================================================

        async void Fusionner()
        {
            if (occupe) return;
            MasquerBandeau();

            // qpdf peut avoir été installé depuis l'ouverture de la fenêtre.
            if (qpdf == null) { qpdf = Qpdf.Trouver(); MajQpdf(); }
            if (qpdf == null) { BandeauQpdfAbsent(); return; }

            if (elements.Count == 0)
            {
                Erreur(L("La liste est vide."), L("Glissez des PDF dans la fenêtre, ou cliquez sur « Choisir un dossier »."));
                return;
            }
            if (elements.Count < 2)
            {
                Erreur(L("Il n'y a qu'un seul fichier dans la liste."), L("Ajoutez au moins un autre PDF à fusionner."));
                return;
            }

            string sortie;
            try { sortie = CheminSortie(); }
            catch (ErreurUtilisateur eu) { Erreur(eu.Titre, eu.Detail); return; }

            bool renommer = chkRenommer.IsChecked == true;
            if (renommer && elements.Select(e => e.Dossier).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                Erreur(L("Le renommage n'est possible que si tous les fichiers sont dans le même dossier."),
                       L("Décochez « Renommer », ou fusionnez un dossier à la fois."));
                return;
            }
            if (elements.Any(e => string.Equals(e.Chemin, sortie, StringComparison.OrdinalIgnoreCase)))
            {
                Erreur(L("Le fichier de sortie est aussi dans la liste."), L("Choisissez un autre nom de fichier de sortie."));
                return;
            }
            if (File.Exists(sortie) && MessageBox.Show(W, L("Le fichier {0} existe déjà.\n\nLe remplacer ?", Q(Path.GetFileName(sortie))),
                                                       "Fusion PDF", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            List<ElementPdf> ordre = elements.ToList();
            Qpdf q = qpdf;
            Occupe(true);
            ResultatFusion r = null;
            Exception echec = null;
            try
            {
                r = await Task.Run(() => Fusion.Executer(q, ordre, sortie, renommer, Progression));
            }
            catch (Exception ex)
            {
                echec = ex;
            }
            Occupe(false);

            if (echec != null)
            {
                ErreurUtilisateur eu = echec as ErreurUtilisateur;
                if (eu != null) Erreur(eu.Titre, eu.Detail);
                else
                {
                    Journal.Ecrire("Erreur inattendue pendant la fusion : " + echec);
                    Erreur(L("Une erreur inattendue a interrompu la fusion."), L("Le détail technique est enregistré dans le journal : {0}", Journal.Chemin));
                }
                MajResume();
                // La ligne en cause est montrée : l'utilisateur n'a pas à la chercher.
                ElementPdf fautif = elements.FirstOrDefault(e => e.Etat == EtatPdf.Protege || e.Etat == EtatPdf.Endommage
                                                                 || e.Etat == EtatPdf.Verrouille || e.Etat == EtatPdf.Introuvable);
                if (fautif != null) Reselectionner(new List<ElementPdf> { fautif });
                AfficherApercu(liste.SelectedItem as ElementPdf);
                return;
            }

            foreach (ElementPdf e in elements)
            {
                string nouveau;
                if (r.CheminsRenommes.TryGetValue(e.Chemin, out nouveau)) e.Chemin = nouveau;
            }
            derniereSortie = r.Sortie;
            txtSortie.Text = "";   // la prochaine fusion est un autre document : nouveau nom obligatoire
            if (!dossierChoisi) dossierSortie = null;
            MajSortieParDefaut();
            MajResume();
            AfficherApercu(liste.SelectedItem as ElementPdf);
            Succes(r);
        }

        /// <summary>Appelé depuis la tâche de fond : on repasse sur le fil de l'interface.</summary>
        void Progression(double pct, string texte)
        {
            W.Dispatcher.BeginInvoke(new Action(delegate
            {
                DoubleAnimation a = new DoubleAnimation(Math.Max(0, Math.Min(100, pct)), new Duration(TimeSpan.FromMilliseconds(220)));
                progres.BeginAnimation(RangeBase_Value, a, HandoffBehavior.SnapshotAndReplace);
                txtProgres.Text = texte;
                txtPourcent.Text = ((int)pct) + " %";
            }));
        }

        static readonly DependencyProperty RangeBase_Value = System.Windows.Controls.Primitives.RangeBase.ValueProperty;

        void Occupe(bool oui)
        {
            occupe = oui;
            zoneDepot.IsEnabled = !oui;
            options.IsEnabled = !oui;
            liste.IsEnabled = !oui;
            btnEnregistrerSous.IsEnabled = !oui;
            if (oui)
            {
                progres.BeginAnimation(RangeBase_Value, null);
                progres.Value = 0;
                txtPourcent.Text = "0 %";
                txtProgres.Text = L("Préparation…");
                zoneProgres.Visibility = Visibility.Visible;
                Apparaitre(zoneProgres);
            }
            else zoneProgres.Visibility = Visibility.Collapsed;
            btnFusionner.Content = oui ? (object)L("Fusion en cours…") : contenuFusionner;
            MajBoutons();
            W.Cursor = oui ? Cursors.AppStarting : null;
        }

        readonly object contenuFusionner;

        // =================================================================
        // Renommage : annulation depuis le journal CSV
        // =================================================================

        async void AnnulerRenommage()
        {
            if (occupe) return;
            MasquerBandeau();
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = L("Choisir le journal de renommage à annuler");
            dlg.Filter = L("Journal de renommage") + " (_RENOMMAGE_*.csv, _RENAMED_*.csv)|_RENOMMAGE_*.csv;_RENAMED_*.csv|CSV (*.csv)|*.csv";
            if (!string.IsNullOrEmpty(dossierSortie) && Directory.Exists(dossierSortie)) dlg.InitialDirectory = dossierSortie;
            if (dlg.ShowDialog(W) != true) return;

            string dossier = Path.GetDirectoryName(dlg.FileName);
            List<LigneRenommage> aRetablir;
            try
            {
                // Ne garder que les lignes encore à rétablir : rejouer l'annulation ne casse rien.
                aRetablir = Renommage.Inverse(Renommage.LireCsv(dlg.FileName))
                    .Where(l => !string.Equals(l.Ancien, l.Nouveau, StringComparison.Ordinal)
                                && File.Exists(Path.Combine(dossier, l.Ancien)))
                    .ToList();
            }
            catch (ErreurUtilisateur eu) { Erreur(eu.Titre, eu.Detail); return; }
            catch (Exception ex) { Erreur(L("Impossible de lire ce journal."), ex.Message); return; }

            if (aRetablir.Count == 0)
            {
                Information(L("Rien à annuler."), L("Les fichiers de ce journal ont déjà leur nom d'origine, ou ne sont plus dans le dossier."));
                return;
            }
            if (MessageBox.Show(W, L("Rendre leur nom d'origine à {0} dans {1} ?", Pl(aRetablir.Count, "fichier", "fichiers"), Q(NomDossier(dossier))),
                                "Fusion PDF", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Occupe(true);
            Progression(50, L("Retour aux noms d'origine…"));
            Dictionary<string, string> faits = null;
            Exception echec = null;
            try { faits = await Task.Run(() => Renommage.Appliquer(dossier, aRetablir)); }
            catch (Exception ex) { echec = ex; }
            Occupe(false);

            if (echec != null)
            {
                ErreurUtilisateur eu = echec as ErreurUtilisateur;
                Journal.Ecrire("Annulation de renommage : " + echec);
                Erreur(eu != null ? eu.Titre : L("L'annulation du renommage a échoué."), eu != null ? eu.Detail : echec.Message);
                return;
            }
            foreach (ElementPdf e in elements)
            {
                string nouveau;
                if (faits.TryGetValue(e.Chemin, out nouveau)) e.Chemin = nouveau;
            }
            AfficherApercu(liste.SelectedItem as ElementPdf);
            Journal.Ecrire("Renommage annulé : " + faits.Count + " fichier(s), journal " + dlg.FileName);
            Bandeau(Couleurs.Vert50, Couleurs.Vert200, Couleurs.Vert800, Couleurs.Vert600, "",
                    Pl(faits.Count, "fichier a retrouvé son nom d'origine.", "fichiers ont retrouvé leur nom d'origine."),
                    L("Dossier : {0}", dossier), false, false);
        }

        // =================================================================
        // qpdf, bandeaux, ouverture
        // =================================================================

        void MajQpdf()
        {
            if (qpdf != null)
            {
                pastilleQpdf.Background = Couleurs.Vert50;
                pastilleQpdf.BorderBrush = Couleurs.Vert200;
                pointQpdf.Fill = Couleurs.Vert600;
                txtQpdf.Foreground = Couleurs.Vert800;
                txtQpdf.Text = L("qpdf {0} prêt", qpdf.Version);
                pastilleQpdf.ToolTip = qpdf.Chemin;
            }
            else
            {
                pastilleQpdf.Background = Couleurs.Rouge50;
                pastilleQpdf.BorderBrush = Couleurs.Rouge200;
                pointQpdf.Fill = Couleurs.Rouge600;
                txtQpdf.Foreground = Couleurs.Rouge800;
                txtQpdf.Text = L("qpdf absent");
                pastilleQpdf.ToolTip = L("qpdf est le moteur de fusion. Il s'installe depuis le Portail d'entreprise.");
            }
        }

        void BandeauQpdfAbsent()
        {
            Bandeau(Couleurs.Ambre50, Couleurs.Ambre200, Couleurs.Ambre800, Couleurs.Ambre600, "",
                    L("qpdf, le moteur de fusion, n'est pas installé sur ce poste."),
                    L("Ouvrez le Portail d'entreprise et installez qpdf. Ensuite, cliquez simplement sur Fusionner : inutile de rouvrir l'application."),
                    false, true);
        }

        void Succes(ResultatFusion r)
        {
            List<string> details = new List<string>();
            details.Add(L("{0} dans {1}.", Pl(r.Fichiers, "fichier fusionné", "fichiers fusionnés"), Q(NomDossier(Path.GetDirectoryName(r.Sortie)))));
            if (r.PagesAttendues > 0 && r.PagesAttendues != r.Pages)
                details.Add(L("Attention : {0} attendues, {1} relues.", Format.Pages(r.PagesAttendues), r.Pages));
            if (r.Avertissements)
                details.Add(L("qpdf a signalé qu'un ou plusieurs fichiers sources sont légèrement abîmés ; le résultat reste lisible."));
            if (r.Csv != null && r.ErreurRenommage == null)
                details.Add(L("{0} ; anciens noms dans {1}.", Pl(r.Renommes, "fichier renommé", "fichiers renommés"), Q(Path.GetFileName(r.Csv))));

            string titre = L("Terminé : {0} ({1})", Path.GetFileName(r.Sortie), Format.Pages(r.Pages));
            if (r.ErreurRenommage != null)
            {
                details.Add(L("Mais le renommage n'a pas eu lieu : {0} {1}", r.ErreurRenommage.Titre, r.ErreurRenommage.Detail));
                Bandeau(Couleurs.Ambre50, Couleurs.Ambre200, Couleurs.Ambre800, Couleurs.Ambre600, "",
                        titre, string.Join(" ", details), true, false);
                return;
            }
            Bandeau(Couleurs.Vert50, Couleurs.Vert200, Couleurs.Vert800, Couleurs.Vert600, "",
                    titre, string.Join(" ", details), true, false);
        }

        void Erreur(string titre, string detail)
        {
            Bandeau(Couleurs.Rouge50, Couleurs.Rouge200, Couleurs.Rouge800, Couleurs.Rouge600, "", titre, detail, false, false);
        }

        void Information(string titre, string detail)
        {
            Bandeau(Couleurs.P50, Couleurs.P200, Couleurs.P700, Couleurs.P500, "", titre, detail, false, false);
        }

        void Bandeau(Brush fond, Brush bord, Brush encre, Brush icone, string glyphe, string titre, string texte,
                     bool boutonsResultat, bool boutonPortail)
        {
            bandeau.Background = fond;
            bandeau.BorderBrush = bord;
            bandeauIcone.Text = glyphe;
            bandeauIcone.Foreground = icone;
            bandeauTitre.Text = titre;
            bandeauTitre.Foreground = encre;
            bandeauTexte.Text = texte ?? "";
            bandeauTexte.Foreground = encre;
            bandeauTexte.Visibility = string.IsNullOrEmpty(texte) ? Visibility.Collapsed : Visibility.Visible;
            btnOuvrirPdf.Visibility = boutonsResultat ? Visibility.Visible : Visibility.Collapsed;
            btnOuvrirDossier.Visibility = boutonsResultat ? Visibility.Visible : Visibility.Collapsed;
            btnPortail.Visibility = boutonPortail ? Visibility.Visible : Visibility.Collapsed;
            bandeau.Visibility = Visibility.Visible;
            Apparaitre(bandeau);
        }

        void MasquerBandeau()
        {
            bandeau.Visibility = Visibility.Collapsed;
        }

        /// <summary>Fondu et léger glissement vers le haut : 180 ms.</summary>
        static void Apparaitre(FrameworkElement el)
        {
            TranslateTransform t = new TranslateTransform(0, 6);
            el.RenderTransform = t;
            DoubleAnimation fondu = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)));
            DoubleAnimation glisse = new DoubleAnimation(6, 0, new Duration(TimeSpan.FromMilliseconds(180)));
            glisse.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            el.BeginAnimation(UIElement.OpacityProperty, fondu);
            t.BeginAnimation(TranslateTransform.YProperty, glisse);
        }

        void Ouvrir(string chemin, bool dossier)
        {
            if (string.IsNullOrEmpty(chemin)) return;
            try
            {
                if (dossier) Process.Start("explorer.exe", "/select,\"" + chemin + "\"");
                else Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Journal.Ecrire("Ouverture impossible : " + chemin + " : " + e.Message);
                Erreur(dossier ? L("Impossible d'ouvrir le dossier.") : L("Aucun lecteur PDF n'a pu ouvrir le fichier."),
                       L("Le fichier est ici : {0}", chemin));
            }
        }

        void OuvrirPortail()
        {
            foreach (string uri in new[] { "companyportal:" })
            {
                try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); return; }
                catch (Exception e) { Journal.Ecrire("Portail d'entreprise : " + uri + " : " + e.Message); }
            }
            Erreur(L("Le Portail d'entreprise n'a pas pu s'ouvrir."), L("Cherchez « Portail d'entreprise » dans le menu Démarrer, puis installez qpdf."));
        }
    }

    public static class Ressources
    {
        public static Stream Flux(string nom)
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(nom);
            if (s == null) throw new InvalidOperationException("Ressource absente de l'exécutable : " + nom);
            return s;
        }

        public static BitmapSource Image(string nom)
        {
            using (Stream s = Flux(nom))
            {
                BitmapDecoder d = BitmapDecoder.Create(s, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                // Pour une icône, la plus grande image ; pour un PNG, la seule.
                BitmapFrame f = d.Frames.OrderByDescending(x => x.PixelWidth).First();
                f.Freeze();
                return f;
            }
        }
    }
}
