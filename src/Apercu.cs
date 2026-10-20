// Fusion PDF - aperçu de la première page, par Windows.Data.Pdf (WinRT).
//
// POURQUOI SANS « await »
// Le csc du .NET Framework sait référencer les .winmd livrés avec Windows
// (C:\Windows\System32\WinMetadata), mais les extensions qui rendent une
// opération WinRT attendable (System.Runtime.WindowsRuntime.dll) exigent le
// Windows.winmd unifié du SDK Windows 10, absent des postes. On attend donc
// chaque opération par son gestionnaire Completed, avec un délai borné.
//
// POURQUOI DEPUIS LA MÉMOIRE
// Chargé depuis le fichier, le document WinRT peut garder celui-ci ouvert
// jusqu'au passage du ramasse-miettes, ce qui bloquerait ensuite un
// renommage. On lit donc le fichier nous-mêmes, et on le referme aussitôt.
//
// Tout échec renvoie null : l'aperçu est un confort, jamais un blocage.

using System;
using System.IO;
using System.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace FusionPDF
{
    public static class Apercu
    {
        const int DelaiMs = 15000;

        /// <summary>Au-delà, pas d'aperçu : le fichier serait lu en entier en mémoire.</summary>
        public const long TailleMax = 80L * 1024 * 1024;

        public static byte[] PremierePage(string chemin, uint largeurPixels)
        {
            try
            {
                byte[] contenu;
                using (FileStream fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length > TailleMax) return null;
                    contenu = new byte[fs.Length];
                    int lu = 0;
                    while (lu < contenu.Length)
                    {
                        int k = fs.Read(contenu, lu, contenu.Length - lu);
                        if (k <= 0) break;
                        lu += k;
                    }
                }

                using (InMemoryRandomAccessStream entree = new InMemoryRandomAccessStream())
                {
                    using (DataWriter w = new DataWriter(entree.GetOutputStreamAt(0)))
                    {
                        w.WriteBytes(contenu);
                        Attendre(w.StoreAsync());
                        Attendre(w.FlushAsync());
                        w.DetachStream();
                    }

                    PdfDocument doc = Attendre(PdfDocument.LoadFromStreamAsync(entree));
                    if (doc.PageCount == 0) return null;

                    using (PdfPage page = doc.GetPage(0))
                    using (InMemoryRandomAccessStream image = new InMemoryRandomAccessStream())
                    {
                        PdfPageRenderOptions o = new PdfPageRenderOptions();
                        o.DestinationWidth = largeurPixels;
                        Attendre(page.RenderToStreamAsync(image, o));

                        uint taille = (uint)image.Size;
                        using (DataReader r = new DataReader(image.GetInputStreamAt(0)))
                        {
                            Attendre(r.LoadAsync(taille));
                            byte[] png = new byte[taille];
                            r.ReadBytes(png);
                            return png;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Journal.Ecrire("Aperçu indisponible pour " + chemin + " : 0x" + e.HResult.ToString("X8") + " " + e.Message);
                return null;
            }
        }

        static T Attendre<T>(IAsyncOperation<T> op)
        {
            using (ManualResetEvent fin = new ManualResetEvent(false))
            {
                op.Completed = delegate { fin.Set(); };
                if (!fin.WaitOne(DelaiMs)) { op.Cancel(); throw new TimeoutException("Aperçu : délai dépassé."); }
                if (op.Status == AsyncStatus.Error) throw op.ErrorCode;
                if (op.Status != AsyncStatus.Completed) throw new OperationCanceledException();
                return op.GetResults();
            }
        }

        static void Attendre(IAsyncAction op)
        {
            using (ManualResetEvent fin = new ManualResetEvent(false))
            {
                op.Completed = delegate { fin.Set(); };
                if (!fin.WaitOne(DelaiMs)) { op.Cancel(); throw new TimeoutException("Aperçu : délai dépassé."); }
                if (op.Status == AsyncStatus.Error) throw op.ErrorCode;
                if (op.Status != AsyncStatus.Completed) throw new OperationCanceledException();
            }
        }
    }
}
