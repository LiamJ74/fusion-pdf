// Fusion PDF - moteur : qpdf, OneDrive, verrous, renommage.
// Aucune dépendance à l'interface : l'autotest l'exerce tel quel.
//
// C# 5 (csc du .NET Framework) : pas de $"", pas de ?., pas de nameof.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FusionPDF
{
    /// <summary>Erreur montrée telle quelle : une phrase en clair, jamais de pile d'appels.</summary>
    public sealed class ErreurUtilisateur : Exception
    {
        public string Titre { get; private set; }
        public string Detail { get; private set; }

        public ErreurUtilisateur(string titre, string detail) : base(titre + " " + detail)
        {
            Titre = titre;
            Detail = detail;
        }
    }

    /// <summary>Journal par utilisateur : %LOCALAPPDATA%\FusionPDF\FusionPDF.log.</summary>
    public static class Journal
    {
        static readonly object verrou = new object();

        public static string Chemin
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                    "FusionPDF", "FusionPDF.log");
            }
        }

        public static void Ecrire(string ligne)
        {
            try
            {
                lock (verrou)
                {
                    string c = Chemin;
                    Directory.CreateDirectory(Path.GetDirectoryName(c));
                    FileInfo fi = new FileInfo(c);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    {
                        File.Copy(c, c + ".old", true);
                        File.Delete(c);
                    }
                    File.AppendAllText(c, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                                          + " - " + ligne + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Un journal impossible à écrire ne doit jamais empêcher une fusion.
            }
        }
    }

    /// <summary>
    /// Tri naturel, celui de l'Explorateur : « page9 » avant « page10 ».
    /// C'est ce qui rend les préfixes 001_ inutiles pour imposer l'ordre.
    /// </summary>
    public sealed class TriNaturel : IComparer<string>
    {
        public static readonly TriNaturel Instance = new TriNaturel();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int StrCmpLogicalW(string a, string b);

        public int Compare(string a, string b)
        {
            return StrCmpLogicalW(a ?? "", b ?? "");
        }
    }

    public static class Format
    {
        public static string Taille(long octets)
        {
            CultureInfo c = Langue.Culture;
            if (octets < 1024) return octets.ToString(c) + " " + Langue.L("o");
            if (octets < 1024 * 1024) return Math.Max(1, (int)Math.Round(octets / 1024.0)).ToString(c) + " " + Langue.L("Ko");
            if (octets < 1024L * 1024 * 1024) return (octets / (1024.0 * 1024)).ToString("0.0", c) + " " + Langue.L("Mo");
            return (octets / (1024.0 * 1024 * 1024)).ToString("0.00", c) + " " + Langue.L("Go");
        }

        public static string Pages(int n)
        {
            return Langue.Pl(n, "page", "pages");
        }

        public static string Date(DateTime d)
        {
            return Langue.L("{0:dd/MM/yyyy HH:mm}", d);
        }
    }

    /// <summary>
    /// OneDrive « fichiers à la demande » : un fichier peut n'être qu'un emplacement
    /// réservé. L'icône est là, le contenu est dans le cloud, et qpdf échoue dessus.
    /// </summary>
    public static class OneDrive
    {
        const int FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000;
        const int FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000;
        const int Masque = FILE_ATTRIBUTE_OFFLINE | FILE_ATTRIBUTE_RECALL_ON_OPEN | FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS;

        public static bool EstEnLigneSeulement(string chemin)
        {
            try { return ((int)File.GetAttributes(chemin) & Masque) != 0; }
            catch { return false; }
        }

        /// <summary>Lire le fichier en entier déclenche son téléchargement complet.</summary>
        public static void Telecharger(string chemin)
        {
            byte[] tampon = new byte[1 << 20];
            using (FileStream fs = new FileStream(chemin, FileMode.Open, FileAccess.Read,
                                                  FileShare.ReadWrite | FileShare.Delete, tampon.Length))
            {
                while (fs.Read(tampon, 0, tampon.Length) > 0) { }
            }
        }
    }

    public static class Fichiers
    {
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;

        /// <summary>
        /// exclusif = false : un autre programme interdit-il la lecture ?
        /// exclusif = true : le fichier est-il ouvert ailleurs (ce qui bloque un renommage) ?
        /// </summary>
        public static bool EstVerrouille(string chemin, bool exclusif)
        {
            try
            {
                FileShare partage = exclusif ? FileShare.None : (FileShare.ReadWrite | FileShare.Delete);
                using (new FileStream(chemin, FileMode.Open, FileAccess.Read, partage)) { }
                return false;
            }
            catch (IOException e)
            {
                int code = e.HResult & 0xFFFF;
                return code == ERROR_SHARING_VIOLATION || code == ERROR_LOCK_VIOLATION;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static string Horodatage()
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
        }

        // Noms des fichiers produits, dans la langue de l'interface. Les deux
        // langues sont reconnues partout : un dossier peut avoir servi aux deux.
        public static string PrefixeSortie { get { return Langue.Anglais ? "_MERGED_" : "_FUSION_"; } }
        public static string PrefixeRenommage { get { return Langue.Anglais ? "_RENAMED_" : "_RENOMMAGE_"; } }

        /// <summary>Un PDF produit par Fusion PDF : jamais repris quand on charge un dossier.</summary>
        public static bool EstUneSortie(string nom)
        {
            foreach (string p in new[] { "_FUSION_", "_MERGED_", ".~FusionPDF-" })
                if (nom.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Nom libre dans le dossier : « x.pdf », sinon « x (2).pdf », « x (3).pdf »…</summary>
        public static string NomLibre(string dossier, string nom)
        {
            string baseNom = Path.GetFileNameWithoutExtension(nom);
            string ext = Path.GetExtension(nom);
            string candidat = nom;
            for (int i = 2; File.Exists(Path.Combine(dossier, candidat)); i++)
                candidat = baseNom + " (" + i.ToString(CultureInfo.InvariantCulture) + ")" + ext;
            return candidat;
        }

        public static string Court()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    public enum EtatPdf { Lecture, Pret, EnLigne, Protege, Endommage, Verrouille, Introuvable }

    public sealed class InfoPages
    {
        public int Pages = -1;
        public EtatPdf Etat = EtatPdf.Endommage;
        public string Detail = "";
    }

    public sealed class ResultatProcessus
    {
        public int Code;
        public bool DelaiDepasse;
        public string Sortie = "";
        public string Erreurs = "";
    }

    /// <summary>
    /// qpdf, installé par l'application Intune « qpdf » (QPDF.QPDF via WAU).
    ///
    /// Deux pièges constatés le 06/10/2026 sur un poste réel :
    ///  - l'installeur ne met PAS qpdf dans le PATH ;
    ///  - le dossier porte la version : C:\Program Files\qpdf 12.4.2\bin.
    /// Et InstallLocation est VIDE dans le registre : on repart de UninstallString.
    /// </summary>
    public sealed class Qpdf
    {
        public string Chemin { get; private set; }
        public string Version { get; private set; }

        static readonly Regex MotifNom = new Regex(@"^qpdf(\s|$)", RegexOptions.IgnoreCase);
        static readonly Regex MotifVersion = new Regex(@"(\d+(\.\d+)+)");
        static readonly Regex MotifProgres = new Regex(@"write progress:\s*(\d+)\s*%");

        Qpdf(string chemin, string version)
        {
            Chemin = chemin;
            Version = version;
        }

        public static Qpdf Trouver()
        {
            string version = null;
            string exe = ParRegistre(out version);
            if (exe == null) exe = ParProgramFiles();
            if (exe == null) exe = ParPath();
            if (exe == null) return null;

            if (string.IsNullOrEmpty(version))
            {
                Match m = MotifVersion.Match(exe);
                version = m.Success ? m.Groups[1].Value : "";
            }
            return new Qpdf(exe, version);
        }

        static string ParRegistre(out string version)
        {
            version = null;
            RegistryHive[] ruches = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] vues = { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (RegistryHive ruche in ruches)
            {
                foreach (RegistryView vue in vues)
                {
                    try
                    {
                        using (RegistryKey racine = RegistryKey.OpenBaseKey(ruche, vue))
                        using (RegistryKey u = racine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (u == null) continue;
                            foreach (string n in u.GetSubKeyNames())
                            {
                                using (RegistryKey k = u.OpenSubKey(n))
                                {
                                    if (k == null) continue;
                                    string nom = k.GetValue("DisplayName") as string;
                                    if (nom == null || !MotifNom.IsMatch(nom)) continue;

                                    string dossier = k.GetValue("InstallLocation") as string;
                                    if (string.IsNullOrEmpty(dossier))
                                    {
                                        string us = k.GetValue("UninstallString") as string;
                                        string e = ExtraireExe(us);
                                        if (e != null) dossier = Path.GetDirectoryName(e);
                                    }
                                    string exe = ChercherExe(dossier);
                                    if (exe != null)
                                    {
                                        version = k.GetValue("DisplayVersion") as string;
                                        return exe;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ruche ou vue illisible : on passe à la suivante.
                    }
                }
            }
            return null;
        }

        static string ParProgramFiles()
        {
            List<string> racines = new List<string>();
            foreach (string v in new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
            {
                string r = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(r) && !racines.Contains(r, StringComparer.OrdinalIgnoreCase)) racines.Add(r);
            }
            foreach (string r in racines)
            {
                string[] dossiers;
                try { dossiers = Directory.GetDirectories(r, "qpdf*"); }
                catch { continue; }
                // La version la plus récente d'abord : « qpdf 12.10 » passe avant « qpdf 12.9 ».
                foreach (string d in dossiers.OrderByDescending(x => Path.GetFileName(x), TriNaturel.Instance))
                {
                    string exe = ChercherExe(d);
                    if (exe != null) return exe;
                }
            }
            return null;
        }

        static string ParPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string d in path.Split(';'))
            {
                try
                {
                    string c = Path.Combine(d.Trim().Trim('"'), "qpdf.exe");
                    if (File.Exists(c)) return c;
                }
                catch { }
            }
            return null;
        }

        static string ExtraireExe(string ligne)
        {
            if (string.IsNullOrEmpty(ligne)) return null;
            ligne = ligne.Trim();
            if (ligne.StartsWith("\""))
            {
                int fin = ligne.IndexOf('"', 1);
                return fin > 1 ? ligne.Substring(1, fin - 1) : null;
            }
            int i = ligne.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return i > 0 ? ligne.Substring(0, i + 4) : ligne;
        }

        static string ChercherExe(string dossier)
        {
            if (string.IsNullOrEmpty(dossier)) return null;
            try
            {
                if (!Directory.Exists(dossier)) return null;
                foreach (string c in new[] { Path.Combine(dossier, "bin", "qpdf.exe"), Path.Combine(dossier, "qpdf.exe") })
                    if (File.Exists(c)) return c;
                return Directory.EnumerateFiles(dossier, "qpdf.exe", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        static string Guillemets(string s)
        {
            return "\"" + s + "\"";
        }

        public ResultatProcessus Executer(string arguments, int delaiMs, Action<string> parLigne)
        {
            ProcessStartInfo psi = new ProcessStartInfo(Chemin, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder so = new StringBuilder();
            StringBuilder se = new StringBuilder();
            ResultatProcessus r = new ResultatProcessus();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (so) so.AppendLine(e.Data);
                    if (parLigne != null) parLigne(e.Data);
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (se) se.AppendLine(e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(delaiMs))
                {
                    try { p.Kill(); } catch { }
                    r.DelaiDepasse = true;
                    r.Code = -1;
                }
                else
                {
                    p.WaitForExit();   // vide les flux asynchrones
                    r.Code = p.ExitCode;
                }
            }
            lock (so) r.Sortie = so.ToString();
            lock (se) r.Erreurs = se.ToString();
            return r;
        }

        /// <summary>
        /// Nombre de pages. Sert aussi de contrôle : un PDF protégé par mot de passe
        /// ou endommagé est repéré ici, avant la fusion, et nommé.
        /// </summary>
        public InfoPages NombrePages(string chemin)
        {
            InfoPages ip = new InfoPages();
            ResultatProcessus r = Executer("--show-npages " + Guillemets(chemin), 60000, null);
            int n;
            string premiere = (r.Sortie ?? "").Trim().Split('\n')[0].Trim();
            if ((r.Code == 0 || r.Code == 3) && int.TryParse(premiere, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                ip.Pages = n;
                ip.Etat = EtatPdf.Pret;
                return ip;
            }
            ip.Detail = r.Erreurs.Trim();
            ip.Etat = r.Erreurs.IndexOf("invalid password", StringComparison.OrdinalIgnoreCase) >= 0
                ? EtatPdf.Protege
                : EtatPdf.Endommage;
            return ip;
        }

        /// <summary>
        /// Fusion. Les arguments passent par un fichier @ : 150 chemins OneDrive
        /// dépassent la limite de 32 767 caractères d'une ligne de commande.
        /// qpdf lit ce fichier en UTF-8 (vérifié avec accents, crochets et tiret long).
        /// </summary>
        public ResultatProcessus Fusionner(IList<string> sources, string sortie, int delaiMs, Action<int> progression)
        {
            string fichierArgs = Path.Combine(Path.GetTempPath(), "FusionPDF-" + Fichiers.Court() + ".txt");
            List<string> lignes = new List<string> { "--empty", "--progress", "--pages" };
            foreach (string s in sources)
            {
                lignes.Add(s);
                lignes.Add("1-z");   // toutes les pages, explicitement
            }
            lignes.Add("--");
            lignes.Add(sortie);
            File.WriteAllLines(fichierArgs, lignes, new UTF8Encoding(false));
            try
            {
                return Executer(Guillemets("@" + fichierArgs), delaiMs, delegate(string ligne)
                {
                    Match m = MotifProgres.Match(ligne);
                    if (m.Success && progression != null)
                        progression(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                });
            }
            finally
            {
                try { File.Delete(fichierArgs); } catch { }
            }
        }

        /// <summary>
        /// Chiffre un PDF : sert uniquement à l'autotest, pour vérifier que
        /// l'application reconnaît un fichier protégé.
        /// </summary>
        public ResultatProcessus Chiffrer(string source, string sortie, string motDePasse)
        {
            return Executer("--encrypt " + motDePasse + " " + motDePasse + " 256 -- "
                            + Guillemets(source) + " " + Guillemets(sortie), 60000, null);
        }
    }

    public sealed class LigneRenommage
    {
        public string Ancien;
        public string Nouveau;

        public LigneRenommage(string ancien, string nouveau)
        {
            Ancien = ancien;
            Nouveau = nouveau;
        }
    }

    /// <summary>
    /// Renommage 001_, 002_… repris de Fusionner-PDF.ps1 (testé le 06/10/2026) :
    ///  - l'ancien nom est consigné dans _RENOMMAGE_&lt;date&gt;.csv, déposé à côté ;
    ///  - un préfixe déjà posé est retiré avant d'en poser un nouveau (pas de 001_001_) ;
    ///  - deux temps, via des noms temporaires : un fichier n'en écrase jamais un autre.
    /// Ajouts : contrôle des collisions avant de toucher à quoi que ce soit, et
    /// retour aux noms d'origine si une étape échoue.
    /// </summary>
    public static class Renommage
    {
        static readonly Regex Prefixe = new Regex(@"^\d+_");

        public static List<LigneRenommage> Plan(IList<string> nomsDansLOrdre)
        {
            int largeur = Math.Max(3, nomsDansLOrdre.Count.ToString(CultureInfo.InvariantCulture).Length);
            List<LigneRenommage> plan = new List<LigneRenommage>();
            for (int i = 0; i < nomsDansLOrdre.Count; i++)
            {
                string ancien = nomsDansLOrdre[i];
                string sansPrefixe = Prefixe.Replace(ancien, "", 1);
                string nouveau = (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(largeur, '0') + "_" + sansPrefixe;
                plan.Add(new LigneRenommage(ancien, nouveau));
            }
            return plan;
        }

        public static List<LigneRenommage> Inverse(IEnumerable<LigneRenommage> plan)
        {
            return plan.Select(l => new LigneRenommage(l.Nouveau, l.Ancien)).ToList();
        }

        static string Champ(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Même format que Export-Csv de PowerShell 5.1 : UTF-8 avec BOM, champs
        /// entre guillemets. En-tête Ancien/Nouveau, ou Old/New en anglais : la
        /// lecture accepte les deux.
        /// </summary>
        public static string EcrireCsv(string dossier, IList<LigneRenommage> plan)
        {
            string nom = Fichiers.NomLibre(dossier, Fichiers.PrefixeRenommage + Fichiers.Horodatage() + ".csv");
            string chemin = Path.Combine(dossier, nom);
            StringBuilder sb = new StringBuilder();
            sb.Append(Langue.Anglais ? "\"Old\",\"New\"\r\n" : "\"Ancien\",\"Nouveau\"\r\n");
            foreach (LigneRenommage l in plan) sb.Append(Champ(l.Ancien)).Append(',').Append(Champ(l.Nouveau)).Append("\r\n");
            File.WriteAllText(chemin, sb.ToString(), new UTF8Encoding(true));
            return chemin;
        }

        public static List<LigneRenommage> LireCsv(string chemin)
        {
            List<List<string>> lignes = AnalyserCsv(File.ReadAllText(chemin, Encoding.UTF8));
            if (lignes.Count == 0) throw new ErreurUtilisateur(Langue.L("Journal de renommage vide."), Langue.L("Le fichier choisi ne contient aucune ligne."));
            int iA = lignes[0].FindIndex(c => c.Trim().Equals("Ancien", StringComparison.OrdinalIgnoreCase) || c.Trim().Equals("Old", StringComparison.OrdinalIgnoreCase));
            int iN = lignes[0].FindIndex(c => c.Trim().Equals("Nouveau", StringComparison.OrdinalIgnoreCase) || c.Trim().Equals("New", StringComparison.OrdinalIgnoreCase));
            if (iA < 0 || iN < 0)
                throw new ErreurUtilisateur(Langue.L("Ce fichier n'est pas un journal de renommage."),
                                            Langue.L("Choisissez un fichier {0}<date>.csv produit par Fusion PDF.", Fichiers.PrefixeRenommage));
            List<LigneRenommage> plan = new List<LigneRenommage>();
            foreach (List<string> l in lignes.Skip(1))
            {
                if (l.Count <= Math.Max(iA, iN)) continue;
                if (string.IsNullOrEmpty(l[iA]) || string.IsNullOrEmpty(l[iN])) continue;
                plan.Add(new LigneRenommage(l[iA], l[iN]));
            }
            return plan;
        }

        static List<List<string>> AnalyserCsv(string texte)
        {
            List<List<string>> lignes = new List<List<string>>();
            List<string> ligne = new List<string>();
            StringBuilder champ = new StringBuilder();
            bool entreGuillemets = false;
            for (int i = 0; i < texte.Length; i++)
            {
                char c = texte[i];
                if (entreGuillemets)
                {
                    if (c == '"' && i + 1 < texte.Length && texte[i + 1] == '"') { champ.Append('"'); i++; }
                    else if (c == '"') entreGuillemets = false;
                    else champ.Append(c);
                }
                else if (c == '"') entreGuillemets = true;
                else if (c == ',') { ligne.Add(champ.ToString()); champ.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    ligne.Add(champ.ToString()); champ.Clear();
                    if (ligne.Count > 1 || ligne[0].Length > 0) lignes.Add(ligne);
                    ligne = new List<string>();
                }
                else if (c != '\uFEFF') champ.Append(c);
            }
            ligne.Add(champ.ToString());
            if (ligne.Count > 1 || ligne[0].Length > 0) lignes.Add(ligne);
            return lignes;
        }

        /// <summary>
        /// Applique le plan dans un dossier. Renvoie, pour chaque fichier renommé,
        /// ancien chemin complet -> nouveau chemin complet.
        /// </summary>
        public static Dictionary<string, string> Appliquer(string dossier, IList<LigneRenommage> plan)
        {
            List<LigneRenommage> utiles = plan.Where(l => !string.Equals(l.Ancien, l.Nouveau, StringComparison.Ordinal)).ToList();
            Dictionary<string, string> faits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (utiles.Count == 0) return faits;

            // Contrôles AVANT toute écriture : rien n'est touché si l'un échoue.
            Dictionary<string, bool> anciens = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (LigneRenommage l in utiles)
            {
                string c = Path.Combine(dossier, l.Ancien);
                if (!File.Exists(c))
                    throw new ErreurUtilisateur(Langue.L("Fichier introuvable : {0}.", Langue.L("« {0} »", l.Ancien)),
                                                Langue.L("Il a été déplacé ou renommé entre-temps. Aucun fichier n'a été renommé."));
                anciens[l.Ancien] = true;
            }
            foreach (LigneRenommage l in utiles)
            {
                if (File.Exists(Path.Combine(dossier, l.Nouveau)) && !anciens.ContainsKey(l.Nouveau))
                    throw new ErreurUtilisateur(Langue.L("Un fichier {0} existe déjà dans le dossier.", Langue.L("« {0} »", l.Nouveau)),
                                                Langue.L("Le renommage l'aurait écrasé : aucun fichier n'a été renommé."));
            }
            foreach (LigneRenommage l in utiles)
            {
                if (Fichiers.EstVerrouille(Path.Combine(dossier, l.Ancien), true))
                    throw new ErreurUtilisateur(Langue.L("{0} est ouvert dans un autre programme.", Langue.L("« {0} »", l.Ancien)),
                                                Langue.L("Fermez-le (par exemple dans votre lecteur PDF), puis réessayez. Aucun fichier n'a été renommé."));
            }

            // Pile des renommages effectués : [chemin actuel, chemin d'origine].
            List<string[]> pile = new List<string[]>();
            List<string[]> temporaires = new List<string[]>();
            try
            {
                foreach (LigneRenommage l in utiles)
                {
                    string origine = Path.Combine(dossier, l.Ancien);
                    string temp = Path.Combine(dossier, "~tmp_" + Fichiers.Court() + "_" + l.Nouveau);
                    File.Move(origine, temp);
                    pile.Add(new[] { temp, origine });
                    temporaires.Add(new[] { temp, Path.Combine(dossier, l.Nouveau), origine });
                }
                foreach (string[] t in temporaires)
                {
                    File.Move(t[0], t[1]);
                    string[] entree = pile.First(p => p[0] == t[0]);
                    entree[0] = t[1];
                }
            }
            catch (Exception e)
            {
                Journal.Ecrire("Renommage interrompu, retour aux noms d'origine : " + e);
                List<string> nonRetablis = new List<string>();
                for (int i = pile.Count - 1; i >= 0; i--)
                {
                    try { File.Move(pile[i][0], pile[i][1]); }
                    catch { nonRetablis.Add(Path.GetFileName(pile[i][0])); }
                }
                if (nonRetablis.Count > 0)
                    throw new ErreurUtilisateur(Langue.L("Le renommage a échoué, et {0} fichier(s) n'ont pas pu reprendre leur nom.", nonRetablis.Count),
                                                Langue.L("Fichiers concernés : {0}. Le journal de renommage du dossier donne les anciens noms.", string.Join(", ", nonRetablis)));
                throw new ErreurUtilisateur(Langue.L("Le renommage a échoué."),
                                            Langue.L("Tous les fichiers ont retrouvé leur nom d'origine. Cause : {0}", e.Message));
            }

            foreach (string[] t in temporaires) faits[t[2]] = t[1];
            return faits;
        }
    }
}
