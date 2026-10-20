// Fusion PDF - point d'entrée.
//
//   FusionPDF.exe                      fenêtre vide (menu Démarrer)
//   FusionPDF.exe "<dossier>"          fenêtre remplie (clic droit sur un dossier)
//   FusionPDF.exe "<a.pdf>" "<b.pdf>"  fenêtre remplie avec ces fichiers
//   FusionPDF.exe --autotest <rapport> contrôles sans fenêtre, code 0 si tout passe
//                                      (utilisé par Build-FusionPDF.ps1)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FusionPDF
{
    public static class Programme
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--autotest")
                return Terminer(Autotest.Executer(args[1]));

            // --lang fr|en impose la langue ; sinon, celle de Windows.
            List<string> reste = new List<string>(args);
            string impose = null;
            int iLang = reste.FindIndex(a => a.Equals("--lang", StringComparison.OrdinalIgnoreCase));
            if (iLang >= 0 && iLang + 1 < reste.Count) { impose = reste[iLang + 1]; reste.RemoveRange(iLang, 2); }
            Langue.Choisir(impose);

            // --liste <fichier> : relance après un changement de langue, la
            // liste en cours y est écrite dans son ordre, un chemin par ligne.
            int iListe = reste.FindIndex(a => a.Equals("--liste", StringComparison.OrdinalIgnoreCase));
            if (iListe >= 0 && iListe + 1 < reste.Count)
            {
                string fichierListe = reste[iListe + 1];
                reste.RemoveRange(iListe, 2);
                try
                {
                    reste.AddRange(File.ReadAllLines(fichierListe, Encoding.UTF8));
                    File.Delete(fichierListe);
                }
                catch (Exception e) { Journal.Ecrire("Liste de relance illisible : " + e.Message); }
            }
            args = reste.ToArray();

            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Journal.Ecrire("Exception non gérée : " + e.ExceptionObject);
            };
            TaskScheduler.UnobservedTaskException += delegate(object s, UnobservedTaskExceptionEventArgs e)
            {
                Journal.Ecrire("Tâche en échec non observée : " + e.Exception);
                e.SetObserved();
            };

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                Journal.Ecrire("Erreur inattendue : " + e.Exception);
                MessageBox.Show(Langue.L("Une erreur inattendue s'est produite. L'application reste ouverte.\n\nLe détail technique est enregistré dans le journal :\n{0}", Journal.Chemin),
                                "Fusion PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };

            Fenetre f;
            try
            {
                f = new Fenetre();
            }
            catch (Exception e)
            {
                Journal.Ecrire("Démarrage impossible : " + e);
                MessageBox.Show(Langue.L("Fusion PDF n'a pas pu démarrer.\n\nLe détail technique est enregistré dans le journal :\n{0}", Journal.Chemin),
                                "Fusion PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            List<string> chemins = args.Select(NettoyerArgument).Where(a => a.Length > 0).ToList();
            Journal.Ecrire("Démarrage " + Version + (Langue.Anglais ? " [en]" : " [fr]") + (chemins.Count > 0 ? " : " + string.Join(" | ", chemins) : ""));
            f.W.Loaded += delegate { if (chemins.Count > 0) f.Charger(chemins); };
            int code = app.Run(f.W);
            Journal.Ecrire("Fermeture");
            return Terminer(code);
        }

        /// <summary>
        /// Fin du processus sans passer par le déchargement des DLL.
        ///
        /// Constaté sur un poste de test : après un aperçu, le pilote
        /// graphique Intel (igd10umt64xe.dll, chargé par le moteur PDF de Windows
        /// via Direct3D 11) plante au démontage du processus, code 0xC0000409.
        /// Tout le travail est fini à ce stade - fichiers fermés, journal écrit -
        /// mais Windows afficherait un rapport d'erreur à chaque fermeture.
        /// </summary>
        static int Terminer(int code)
        {
            try { TerminateProcess(GetCurrentProcess(), (uint)code); } catch { }
            return code;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool TerminateProcess(IntPtr processus, uint code);

        public static string Version
        {
            get { return typeof(Programme).Assembly.GetName().Version.ToString(); }
        }

        /// <summary>
        /// Le clic droit sur un lecteur passe "C:\" : la barre oblique inverse
        /// échappe alors le guillemet final, et Windows livre « C:" ».
        /// </summary>
        public static string NettoyerArgument(string a)
        {
            if (a == null) return "";
            a = a.Trim().Trim('"');
            if (a.Length == 2 && a[1] == ':') a += "\\";
            return a;
        }
    }

    /// <summary>
    /// Contrôles lancés par le script de build, sans fenêtre visible : le XAML
    /// se charge, le tri, le renommage et son annulation donnent le résultat
    /// attendu sur de vrais fichiers, et, si qpdf est présent, une vraie
    /// fusion, un fichier protégé et un fichier endommagé sont reconnus.
    /// </summary>
    public static class Autotest
    {
        static readonly List<string> rapport = new List<string>();
        static int echecs;

        static void Verifier(string nom, Func<string> test)
        {
            try
            {
                string detail = test();
                rapport.Add("OK     " + nom + (string.IsNullOrEmpty(detail) ? "" : " : " + detail));
            }
            catch (Exception e)
            {
                echecs++;
                rapport.Add("ECHEC  " + nom + " : " + e.GetType().Name + " " + e.Message);
            }
        }

        static void Attendu(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static int Executer(string fichierRapport)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "FusionPDF-autotest-" + Fichiers.Court());
            Directory.CreateDirectory(tmp);
            try
            {
                rapport.Add("Fusion PDF " + Programme.Version + " - autotest du " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                Verifier("Interface anglaise : XAML traduit et fenêtre (sans affichage)", delegate
                {
                    Langue.Anglais = true;
                    try
                    {
                        Fenetre f = new Fenetre();
                        Attendu(f.W != null && f.W.Title == "Fusion PDF", "fenêtre non construite");
                        Attendu(Langue.L("La liste est vide.") != "La liste est vide.", "traduction non appliquée");
                        Attendu(Format.Taille(2048) == "2 KB", Format.Taille(2048));
                        return Langue.NombreDeTraductions + " phrases traduites";
                    }
                    finally { Langue.Anglais = false; }
                });

                // Les contrôles suivants vérifient des messages français.
                Langue.Anglais = false;

                Verifier("XAML et fenêtre (sans affichage)", delegate
                {
                    Fenetre f = new Fenetre();
                    Attendu(f.W != null && f.W.Title == "Fusion PDF", "fenêtre non construite");
                    string affichee = ((System.Windows.Controls.TextBlock)f.W.FindName("TxtVersion")).Text;
                    string attendue = "Fusion PDF " + typeof(Programme).Assembly.GetName().Version.ToString(3);
                    Attendu(affichee == attendue, "version affichée : '" + affichee + "', attendue '" + attendue + "'");
                    return "MainWindow.xaml chargé, contrôles retrouvés, version affichée « " + affichee + " »";
                });

                Verifier("Tri naturel", delegate
                {
                    string[] noms = { "page10.pdf", "Page1.pdf", "page9.pdf", "annexe10.pdf", "annexe2.pdf", "page100.pdf" };
                    string obtenu = string.Join(" ", noms.OrderBy(n => n, TriNaturel.Instance));
                    string attendu = "annexe2.pdf annexe10.pdf Page1.pdf page9.pdf page10.pdf page100.pdf";
                    Attendu(obtenu == attendu, obtenu);
                    return obtenu;
                });

                Verifier("Parcours des sous-dossiers (ordre, cachés, fusions, jonction qui boucle)", delegate
                {
                    string r = Path.Combine(tmp, "arbre [x]");
                    foreach (string sd in new[] { "", "sous2", "sous2\\profond", "sous10", ".cache" }) Directory.CreateDirectory(Path.Combine(r, sd));
                    foreach (string f in new[] { "b.pdf", "a10.pdf", "a9.pdf", "_FUSION_ancien.pdf", "note.txt",
                                                 "sous2\\x.pdf", "sous2\\profond\\z.pdf", "sous10\\y.pdf", ".cache\\cache.pdf" })
                        File.WriteAllText(Path.Combine(r, f), "x");
                    File.SetAttributes(Path.Combine(r, ".cache"), FileAttributes.Directory | FileAttributes.Hidden);

                    // Jonction sous2\boucle -> racine : sans garde, le parcours tournerait en rond.
                    string jonction = Path.Combine(r, "sous2\\boucle");
                    Process pj = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + jonction + "\" \"" + r + "\"")
                                               { UseShellExecute = false, CreateNoWindow = true });
                    pj.WaitForExit(10000);
                    bool avecJonction = Directory.Exists(jonction);

                    Func<List<string>, string> rel = l => string.Join(" ", l.Select(p => p.Substring(r.Length + 1)));
                    string plat = rel(Fichiers.PdfDuDossier(r, false));
                    Attendu(plat == "a9.pdf a10.pdf b.pdf", plat);
                    string arbre = rel(Fichiers.PdfDuDossier(r, true));
                    Attendu(arbre == "a9.pdf a10.pdf b.pdf sous2\\x.pdf sous2\\profond\\z.pdf sous10\\y.pdf", arbre);
                    Attendu(Fichiers.EstSousDossierDe(Path.Combine(r, "sous2"), r) && !Fichiers.EstSousDossierDe(r, r)
                            && !Fichiers.EstSousDossierDe(r + "-autre", r), "EstSousDossierDe");
                    if (avecJonction) Directory.Delete(jonction);
                    return arbre + (avecJonction ? " ; jonction ignorée" : " ; jonction non créée");
                });

                Verifier("Argument de clic droit sur un lecteur", delegate
                {
                    Attendu(Programme.NettoyerArgument("C:\"") == "C:\\", Programme.NettoyerArgument("C:\""));
                    Attendu(Programme.NettoyerArgument("\"D:\\Dossier [x]\"") == "D:\\Dossier [x]", "guillemets");
                    return "C:\" -> C:\\";
                });

                Verifier("Plan de renommage (préfixe retiré, largeur)", delegate
                {
                    List<LigneRenommage> p = Renommage.Plan(new[] { "003_gamma.pdf", "rapport [final].pdf", "002_beta.pdf", "001_alpha.pdf" });
                    string obtenu = string.Join(" | ", p.Select(l => l.Nouveau));
                    Attendu(obtenu == "001_gamma.pdf | 002_rapport [final].pdf | 003_beta.pdf | 004_alpha.pdf", obtenu);
                    Attendu(Renommage.Plan(Enumerable.Range(1, 1200).Select(i => "f" + i + ".pdf").ToList())[0].Nouveau == "0001_f1.pdf", "largeur 4");
                    return obtenu;
                });

                Verifier("Renommage réel en deux temps, journal CSV, puis annulation", delegate
                {
                    string d = Path.Combine(tmp, "renommage");
                    Directory.CreateDirectory(d);
                    string[] noms = { "001_alpha.pdf", "002_beta.pdf", "003_gamma.pdf", "rapport [final], \"v2\".pdf".Replace("\"", "'") };
                    foreach (string n in noms) File.WriteAllText(Path.Combine(d, n), "contenu de " + n);
                    List<string> ordre = noms.Reverse().ToList();
                    List<LigneRenommage> plan = Renommage.Plan(ordre);
                    string csv = Renommage.EcrireCsv(d, plan);
                    Dictionary<string, string> faits = Renommage.Appliquer(d, plan);
                    Attendu(faits.Count == 4, faits.Count + " renommés");
                    foreach (LigneRenommage l in plan)
                        Attendu(File.ReadAllText(Path.Combine(d, l.Nouveau)) == "contenu de " + l.Ancien, "contenu déplacé : " + l.Nouveau);
                    Attendu(Directory.GetFiles(d, "~tmp_*").Length == 0, "fichier temporaire resté");

                    List<LigneRenommage> relu = Renommage.LireCsv(csv);
                    Attendu(relu.Count == 4 && relu[3].Ancien == plan[3].Ancien, "CSV relu");
                    Renommage.Appliquer(d, Renommage.Inverse(relu));
                    foreach (string n in noms)
                        Attendu(File.ReadAllText(Path.Combine(d, n)) == "contenu de " + n, "non rétabli : " + n);
                    return "4 fichiers renommés puis rétablis, aucun écrasé";
                });

                Verifier("Renommage refusé si un nom cible existe déjà", delegate
                {
                    string d = Path.Combine(tmp, "collision");
                    Directory.CreateDirectory(d);
                    File.WriteAllText(Path.Combine(d, "a.pdf"), "a");
                    File.WriteAllText(Path.Combine(d, "001_a.pdf"), "intrus");
                    try
                    {
                        Renommage.Appliquer(d, new List<LigneRenommage> { new LigneRenommage("a.pdf", "001_a.pdf") });
                        throw new Exception("collision non détectée");
                    }
                    catch (ErreurUtilisateur) { }
                    Attendu(File.ReadAllText(Path.Combine(d, "001_a.pdf")) == "intrus" && File.Exists(Path.Combine(d, "a.pdf")), "fichier touché");
                    return "rien n'a été modifié";
                });

                Verifier("Fichier verrouillé", delegate
                {
                    string f = Path.Combine(tmp, "verrou.pdf");
                    File.WriteAllText(f, "x");
                    using (new FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        Attendu(Fichiers.EstVerrouille(f, false), "verrou exclusif non vu");
                    using (new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                        Attendu(!Fichiers.EstVerrouille(f, false) && Fichiers.EstVerrouille(f, true), "lecteur PDF simulé");
                    Attendu(!Fichiers.EstVerrouille(f, true), "faux positif");
                    return "verrou exclusif et lecteur ouvert distingués";
                });

                Qpdf q = Qpdf.Trouver();
                if (q == null)
                {
                    rapport.Add("INFO   qpdf absent de ce poste : fusion réelle non testée");
                }
                else
                {
                    rapport.Add("INFO   qpdf " + q.Version + " : " + q.Chemin);
                    Verifier("Fusion réelle (crochets, accents, 3 fichiers, 6 pages)", delegate
                    {
                        string d = Path.Combine(tmp, "fusion é [test]");
                        Directory.CreateDirectory(d);
                        List<ElementPdf> l = new List<ElementPdf>();
                        int[] pages = { 1, 2, 3 };
                        for (int i = 0; i < pages.Length; i++)
                        {
                            string f = Path.Combine(d, "pièce [" + (i + 1) + "].pdf");
                            PdfDeTest.Ecrire(f, pages[i]);
                            l.Add(new ElementPdf(f));
                        }
                        string sortie = Path.Combine(d, "_FUSION_test.pdf");
                        ResultatFusion r = Fusion.Executer(q, l, sortie, true, delegate { });
                        Attendu(r.Pages == 6, r.Pages + " pages relues");
                        Attendu(r.Renommes == 3 && File.Exists(Path.Combine(d, "001_pièce [1].pdf")), "renommage après fusion");
                        Attendu(Directory.GetFiles(d, ".~FusionPDF-*").Length == 0, "temporaire resté");
                        return "6 pages relues, 3 sources renommées";
                    });

                    Verifier("PDF protégé par mot de passe reconnu et nommé", delegate
                    {
                        string clair = Path.Combine(tmp, "clair.pdf");
                        string protege = Path.Combine(tmp, "protégé.pdf");
                        PdfDeTest.Ecrire(clair, 1);
                        ResultatProcessus rp = q.Chiffrer(clair, protege, "secret");
                        Attendu(rp.Code == 0, "chiffrement qpdf : " + rp.Code);
                        Attendu(q.NombrePages(protege).Etat == EtatPdf.Protege, "non reconnu");
                        try
                        {
                            Fusion.Executer(q, new List<ElementPdf> { new ElementPdf(clair), new ElementPdf(protege) },
                                            Path.Combine(tmp, "_FUSION_pw.pdf"), false, delegate { });
                            throw new Exception("fusion acceptée");
                        }
                        catch (ErreurUtilisateur eu)
                        {
                            Attendu(eu.Titre.Contains("« protégé.pdf »") && eu.Titre.Contains("mot de passe"), eu.Titre);
                            Attendu(!File.Exists(Path.Combine(tmp, "_FUSION_pw.pdf")), "sortie partielle restée");
                            return eu.Titre;
                        }
                    });

                    Verifier("PDF endommagé reconnu et nommé", delegate
                    {
                        string clair = Path.Combine(tmp, "bon.pdf");
                        string casse = Path.Combine(tmp, "cassé.pdf");
                        PdfDeTest.Ecrire(clair, 1);
                        File.WriteAllText(casse, "ceci n'est pas un PDF");
                        try
                        {
                            Fusion.Executer(q, new List<ElementPdf> { new ElementPdf(clair), new ElementPdf(casse) },
                                            Path.Combine(tmp, "_FUSION_ko.pdf"), false, delegate { });
                            throw new Exception("fusion acceptée");
                        }
                        catch (ErreurUtilisateur eu)
                        {
                            Attendu(eu.Titre.Contains("« cassé.pdf »"), eu.Titre);
                            return eu.Titre;
                        }
                    });

                    Verifier("Aperçu de la première page (Windows.Data.Pdf)", delegate
                    {
                        string f = Path.Combine(tmp, "apercu.pdf");
                        PdfDeTest.Ecrire(f, 2);
                        byte[] png = Apercu.PremierePage(f, 300);
                        Attendu(png != null && png.Length > 500 && png[1] == 'P' && png[2] == 'N' && png[3] == 'G', "pas d'image PNG");
                        File.Delete(f);   // le fichier ne doit pas rester ouvert
                        return png.Length + " octets, fichier relâché";
                    });
                }
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
                rapport.Add(echecs == 0 ? "RESULTAT : tous les controles passent" : "RESULTAT : " + echecs + " controle(s) en echec");
                try { File.WriteAllLines(fichierRapport, rapport, new UTF8Encoding(true)); } catch { }
            }
            return echecs == 0 ? 0 : 1;
        }
    }

    /// <summary>PDF minimal valide, une ligne de texte par page. Sert à l'autotest.</summary>
    public static class PdfDeTest
    {
        public static void Ecrire(string chemin, int pages)
        {
            List<string> objets = new List<string>();
            int police = 3 + 2 * pages;
            objets.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objets.Add("<< /Type /Pages /Kids [" + string.Join(" ", Enumerable.Range(0, pages).Select(i => (3 + 2 * i) + " 0 R")) + "] /Count " + pages + " >>");
            for (int i = 0; i < pages; i++)
            {
                string flux = "BT /F1 32 Tf 60 700 Td (Page " + (i + 1) + ") Tj ET\n";
                objets.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 " + police + " 0 R >> >> /Contents " + (4 + 2 * i) + " 0 R >>");
                objets.Add("<< /Length " + flux.Length + " >>\nstream\n" + flux + "endstream");
            }
            objets.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            StringBuilder sb = new StringBuilder("%PDF-1.4\n");
            List<int> positions = new List<int>();
            for (int k = 0; k < objets.Count; k++)
            {
                positions.Add(sb.Length);
                sb.Append(k + 1).Append(" 0 obj\n").Append(objets[k]).Append("\nendobj\n");
            }
            int xref = sb.Length;
            sb.Append("xref\n0 ").Append(objets.Count + 1).Append("\n0000000000 65535 f \n");
            foreach (int p in positions) sb.Append(p.ToString("D10")).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size ").Append(objets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            File.WriteAllBytes(chemin, Encoding.ASCII.GetBytes(sb.ToString()));
        }
    }
}
