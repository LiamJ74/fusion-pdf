// Fusion PDF - langue de l'interface : français ou anglais.
//
// LE FRANÇAIS EST LA CLÉ
// Les textes restent écrits en français dans le code, entourés de L("…"),
// et dans MainWindow.xaml, entourés de §…§. Le fichier textes.en.txt donne,
// pour chaque phrase française, sa traduction anglaise. Ainsi le code se lit
// comme avant, et une phrase sans traduction se voit au build :
// Build-FusionPDF.ps1 extrait tous les L("…") et §…§ et refuse de compiler
// s'il en manque une.
//
// CHOIX DE LA LANGUE
// Français si Windows est en français, anglais sinon. « --lang fr » ou
// « --lang en » sur la ligne de commande l'impose.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FusionPDF
{
    public static class Langue
    {
        static Dictionary<string, string> anglais;
        static bool estAnglais;

        public static bool Anglais
        {
            get { return estAnglais; }
            set { estAnglais = value; }
        }

        public static CultureInfo Culture
        {
            get { return CultureInfo.GetCultureInfo(estAnglais ? "en-US" : "fr-FR"); }
        }

        /// <summary>Choix mémorisé par l'utilisateur (sélecteur FR | EN de l'en-tête).</summary>
        public static string FichierPreference
        {
            get { return Path.Combine(Path.GetDirectoryName(Journal.Chemin), "langue.txt"); }
        }

        /// <summary>
        /// Ordre de priorité : la ligne de commande (--lang), puis le choix
        /// mémorisé, puis la langue de Windows.
        /// </summary>
        public static void Choisir(string impose)
        {
            string memorise = null;
            try { if (File.Exists(FichierPreference)) memorise = File.ReadAllText(FichierPreference).Trim(); } catch { }
            string choix = !string.IsNullOrEmpty(impose) ? impose : memorise;
            if (string.Equals(choix, "en", StringComparison.OrdinalIgnoreCase)) estAnglais = true;
            else if (string.Equals(choix, "fr", StringComparison.OrdinalIgnoreCase)) estAnglais = false;
            else estAnglais = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr";
        }

        public static void Memoriser(bool anglais)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FichierPreference));
                File.WriteAllText(FichierPreference, anglais ? "en" : "fr");
            }
            catch { }
        }

        /// <summary>Clé normalisée : les espaces insécables du XAML valent des espaces.</summary>
        static string Cle(string fr)
        {
            return fr.Replace("&#160;", " ").Replace(' ', ' ').Trim();
        }

        static Dictionary<string, string> Dictionnaire
        {
            get
            {
                if (anglais != null) return anglais;
                Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
                string texte;
                using (Stream s = Ressources.Flux("FusionPDF.textes.en.txt"))
                using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    texte = r.ReadToEnd();
                string fr = null;
                foreach (string brut in texte.Split('\n'))
                {
                    string ligne = brut.TrimEnd('\r');
                    // Une phrase tient sur une ligne : ses retours à la ligne s'écrivent \n.
                    if (ligne.StartsWith("fr: ")) fr = ligne.Substring(4).Replace("\\n", "\n");
                    else if (ligne.StartsWith("en: ") && fr != null)
                    {
                        d[Cle(fr)] = ligne.Substring(4).Replace("\\n", "\n");
                        fr = null;
                    }
                }
                anglais = d;
                return d;
            }
        }

        /// <summary>Nombre de phrases traduites : sert à l'autotest.</summary>
        public static int NombreDeTraductions
        {
            get { return Dictionnaire.Count; }
        }

        public static bool Traduit(string fr)
        {
            return Dictionnaire.ContainsKey(Cle(fr));
        }

        /// <summary>Le texte dans la langue de l'interface.</summary>
        public static string L(string fr)
        {
            if (!estAnglais) return fr;
            string en;
            if (Dictionnaire.TryGetValue(Cle(fr), out en)) return en;
            Journal.Ecrire("Traduction manquante : " + fr);
            return fr;
        }

        public static string L(string fr, params object[] valeurs)
        {
            return string.Format(Culture, L(fr), valeurs);
        }

        /// <summary>« 3 fichiers », « 1 fichier » : le nombre, puis le mot traduit.</summary>
        public static string Pl(int n, string singulier, string pluriel)
        {
            return n.ToString("#,0", Culture) + " " + L(n > 1 ? pluriel : singulier);
        }

        static readonly Regex Segments = new Regex("§(.*?)§", RegexOptions.Singleline);

        /// <summary>
        /// Traduit le XAML avant son chargement : chaque §…§ est remplacé par
        /// le texte de la langue choisie. Les traductions anglaises peuvent
        /// porter du balisage (<Bold>…</Bold>), écrit comme dans le XAML.
        /// </summary>
        public static string Xaml(string source)
        {
            return Segments.Replace(source, delegate(Match m)
            {
                if (!estAnglais) return m.Groups[1].Value;
                string en;
                if (Dictionnaire.TryGetValue(Cle(m.Groups[1].Value), out en)) return en;
                throw new InvalidOperationException("Traduction anglaise manquante pour le texte d'interface : " + m.Groups[1].Value);
            });
        }
    }
}
