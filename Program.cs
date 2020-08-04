using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Console;
using ColorConsole = Colorful.Console;

namespace Injection_Dossiers_CADASTRE
{
    class Program
    {
        public static string cheminErreur;

        public static string cheminArchiveSource;

        public static string cheminArchiveDestination;

        public static string destination;

        public static string Site_Ancfcc;

        public static string Scan_Ancfcc;
        static void Main(string[] args)
        {
            //Menu
            var helper = new Helper();
            helper.Menu();
            var st = Helper.selectedSite;
            ColorConsole.WriteLine($"Site : {st.Nom}", Color.Cyan);
            
            var cheminSource = st.chemin_source;
            destination = st.chemin_destination;
            cheminArchiveSource = st.chemin_archive_source;
            cheminArchiveDestination = st.chemin_archive_destination;
            cheminErreur = st.chemin_erreur;

            //liste livrables dossiers se termine avec INJ
            var chemin = new DirectoryInfo(cheminSource);
            var Sites = chemin.GetDirectories();
            foreach (var site in Sites)
            {
                if (!site.Name.Contains("INJ"))
                {
                    break;
                }

                Site_Ancfcc = site.Name;
                var CheminTranche = new DirectoryInfo(site.FullName);
                var DossierScan = CheminTranche.GetDirectories();
                foreach (var scan in DossierScan)
                {
                    Scan_Ancfcc = scan.Name;
                    var root = scan;
                    if (root.Exists)
                    {
                        int idlivrable = verifierExistanceDulivrable($"{Site_Ancfcc}_{root.Name}");
                        if (idlivrable != 0)
                        {
                            WriteLine("Récuparation de ID livrable");
                        }
                        else
                        {
                            ColorConsole.WriteLine("Insertion du livrable",Color.Green);
                            insertLivrable("2", (site.Name + ("_" + root.Name)));
                            idlivrable = verifierExistanceDulivrable((site.Name + ("_" + root.Name)));
                        }

                        WriteLine(("Livrable : " + idlivrable));
                        DirectoryInfo[] dossiers = root.GetDirectories();
                        int idLastVue = 0;
                        idLastVue = getFirstIdOfPiece();
                        int countDossier = 0;
                        int totale = dossiers.Length;
                        foreach (var dossier in dossiers)
                        {
                            countDossier++;
                            WriteLine($"Traitement {countDossier} / {totale}");
                            WriteLine(("dossier name : " + dossier.Name));
                            if ((root.Exists && verifierNameDossier(dossier.Name, dossier.FullName)))
                            {
                                if (verifierCarton(dossier.FullName))
                                {
                                    string[] indexes_name = dossier.Name.Split("_");
                                    string user_scan = indexes_name[0];
                                    string namedossier = (indexes_name[1] + ("_" + indexes_name[2]));
                                    string date_scan = indexes_name[3];
                                    var livrableDossierRelPath = Path.Combine(site.Name, scan.Name);
                                    WriteLine(("Verification de l'existance du dossier : " + namedossier));
                                    int idDossier = verifierExistanceDuDossier(namedossier);
                                    if ((idDossier != 0))
                                    {
                                        WriteLine(("Dossier existant : ID dossier : " + idDossier));
                                        WriteLine("Verification de existance des pieces au niveau de la BD");
                                        if (verifierExistanceDespiecesSurunDossierExistant(idDossier))
                                        {
                                            ColorConsole.WriteLine($"Dossier injected : deplacement du TF {namedossier} sur le dossier injected",Color.Yellow);
                                            deplacerCartonErreur(dossier.FullName,  Path.Combine(cheminErreur, livrableDossierRelPath, "injected"));
                                        }
                                        else
                                        {
                                            ColorConsole.WriteLine("Dossier injecteé mais sans aucune piece injectées", Color.Yellow);
                                            WriteLine("Insertion des pieces");
                                            getPathAndBarCode(dossier.FullName, out var pathAndBarcode);
                                            insertPieces(idDossier, pathAndBarcode, namedossier);
                                            var idAndBarcode = getIDandBarcodeofTF(idDossier);
                                            var updateFolder = true;
                                            for (int i = 0; i < pathAndBarcode.Count; i++)
                                            {
                                                //string id = Integer.toString(i);
                                                var indexes = pathAndBarcode[i].Split(";");
                                                var barcode = indexes[0];
                                                var imageSource = indexes[1];
                                                var id_vue = idAndBarcode[barcode];
                                                if (!setodre(id_vue, idLastVue))
                                                {
                                                    updateFolder = false;
                                                    break;
                                                }

                                                var imageDestination = Path.Combine(destination, namedossier, Path.ChangeExtension(imageSource,".tif"));
                                                File.Copy(imageSource, imageDestination);
                                                idLastVue = id_vue;
                                            }

                                            if (updateFolder)
                                            {
                                                ColorConsole.WriteLine("Mise à jour des pièces réussite", Color.Green);
                                                WriteLine("Commencement d'archivage source ");
                                                
                                                if (deplacerCartonErreur(dossier.FullName, Path.Combine(cheminArchiveSource, livrableDossierRelPath)))
                                                {
                                                    ColorConsole.WriteLine("Archivage source réussie", Color.Green);
                                                    WriteLine("Commencement d'archivage destination ");
                                                    string cheminCartonDestination = Path.Combine(destination, namedossier);
                                                    if (CopieCarton(cheminCartonDestination, Path.Combine(cheminArchiveDestination, livrableDossierRelPath)))
                                                    {
                                                        ColorConsole.WriteLine("Archivage destination réussie", Color.Green);
                                                    }
                                                    else
                                                    {
                                                        ColorConsole.WriteLine("Erreur : Archivage destination", Color.Red);
                                                    }

                                                }
                                                else
                                                {
                                                    ColorConsole.WriteLine("Erreur : Archivage source ", Color.Red);
                                                }

                                            }
                                            else
                                            {
                                                ColorConsole.WriteLine("Erreur mise à jour des pieces ", Color.Red);
                                                ColorConsole.WriteLine("Pas d'archivage", Color.Red);
                                            }

                                        }

                                    }
                                    else
                                    {
                                        WriteLine("Creation du dossier sur la BD");
                                        getPathAndBarCode(dossier.FullName, out var pathAndBarcode);
                                        string chemindossier = (destination + ("/"
                                                    + (namedossier + "/")));
                                        if (insertDossier(idlivrable, pathAndBarcode.Count, namedossier, chemindossier, 2, user_scan, date_scan))
                                        {
                                            idDossier = verifierExistanceDuDossier(namedossier);
                                            WriteLine(("ID dossier : " + idDossier));
                                            insertPieces(idDossier, pathAndBarcode, namedossier);
                                            var idAndBarcode = getIDandBarcodeofTF(idDossier);
                                            var updateFolder = true;
                                            for (int i = 0; i < pathAndBarcode.Count; i++)
                                            {
                                                var indexes = pathAndBarcode[i].Split(";");
                                                var barcode = indexes[0];
                                                var imageSource = indexes[1];
                                                var id_vue = idAndBarcode[barcode];
                                                if (!setodre(id_vue, idLastVue))
                                                {
                                                    updateFolder = false;
                                                    break;
                                                }
                                                var imageDestination = Path.Combine(destination, namedossier, Path.ChangeExtension(imageSource, ".tif"));
                                                File.Copy(imageSource, imageDestination);
                                                idLastVue = id_vue;
                                            }

                                            if (updateFolder)
                                            {
                                                ColorConsole.WriteLine("mise a jour des pieces reussite", Color.Green);
                                                WriteLine("commencement d'archivage source ");
                                                if (deplacerCartonErreur(dossier.FullName, Path.Combine(cheminArchiveSource, livrableDossierRelPath)))
                                                {
                                                    ColorConsole.WriteLine("Archivage source reussie", Color.Green);
                                                    WriteLine("Commencement d'archivage destination ");
                                                    string cheminCartonDestination = Path.Combine(destination, namedossier);
                                                    if (CopieCarton(cheminCartonDestination, Path.Combine(cheminArchiveDestination, site.Name, scan.Name)))
                                                    {
                                                        ColorConsole.WriteLine("Archivage destination reussie", Color.Green);
                                                    }
                                                    else
                                                    {
                                                        ColorConsole.WriteLine("Erreur : Archivage destination", Color.Red);
                                                    }
                                                }
                                                else
                                                {
                                                    ColorConsole.WriteLine("Erreur : Archivage source ", Color.Red);
                                                }
                                            }
                                            else
                                            {
                                                ColorConsole.WriteLine("erreur mise a jour des pieces ", Color.Red);
                                                ColorConsole.WriteLine("pas d'archivage", Color.Red);
                                            }
                                        }
                                        else
                                        {
                                            ColorConsole.WriteLine("Erreur insertion dossier", Color.Red);
                                        }
                                    }
                                }
                            }
                            WriteLine("Fin ");
                        }
                    }
                    else
                    {
                        ColorConsole.WriteLine("Erreur : attention le chemin source spicifié n'est pas correct", Color.Red);
                    }
                }                
            }
        }

        public static bool verifierNameDossier(string nameDossier, string cartonPath)
        {
            string[] indexes = nameDossier.Split("_");
            if ((indexes.Length == 4))
            {
                return true;
            }

            ColorConsole.WriteLine("Erreur nomination !!", Color.Red);
            WriteLine("Déplacement du dossier : " + nameDossier);
            deplacerCartonErreur(cartonPath, Path.Combine(cheminErreur, Site_Ancfcc, Scan_Ancfcc , "ErreurNomination"));
            return false;
        }

        public static int getFirstIdOfPiece()
        {
            int idFirstVue = 0;
            string requette = "SELECT ISNULL(MAX(id_vue), 0) as max FROM dbo.TB_Vues";
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.Read, out DataTable dt);
                var valPath = dt.Rows[0]["max"].ToString();
                
                if (valPath == null)
                {
                    idFirstVue = 0;
                }
                else
                {
                    idFirstVue = int.Parse(valPath);
                }
            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                return 0;
            }
            

            return idFirstVue;
        }

        public static bool setodre(int idVue, int valueOrdre)
        {
            var condition = true;
            var requette = $"UPDATE [dbo].[TB_Vues] SET [id_status]=1,[numero_ordre]={valueOrdre} where id_vue={idVue}";
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.CUD, out var _);

            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                condition = false;
            }

            return condition;
        }

        public static Dictionary<string, int> getIDandBarcodeofTF(int idTF)
        {
            var idAndBarcode = new Dictionary<string, int>();
            string requette = ("SELECT [id_vue] ,[bar_code] as num FROM [dbo].[TB_Vues] where id_dossier=" + idTF);
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.Read, out DataTable dt);
                
                    var id_vue = (int)dt.Rows[0]["id_vue"];
                    var bar_code = dt.Rows[0]["num"].ToString();
                    idAndBarcode.Add(bar_code.Trim(), id_vue);
                
            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                return null;
            }

            return idAndBarcode;
        }

        public static bool verifierExistanceDespiecesSurunDossierExistant(int idDossier)
        {
            var condition = false;
            string requette = ("SELECT COUNT(*)as num FROM [dbo].[TB_Vues] where id_dossier=" + idDossier);
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.Read, out DataTable dt);
                    
                var nbrBds = (int)dt.Rows[0]["num"];
                condition = nbrBds != 0;

            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                return false;
            }
            return condition;
        }

        public static int verifierExistanceDuDossier(string nameDossier)
        {
            int idDossier = 0;
            string requette = ("SELECT [id_dossier] FROM [dbo].[TB_Dossier] where name_dossier='"
                        + (nameDossier + "'"));
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.Read, out DataTable dt);
                idDossier = (int)dt.Rows[0]["id_dossier"];
            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                return 0;
            }

            return idDossier;
        }

        public static int verifierExistanceDulivrable(string nameLivrable)
        {
            int idLivrable = 0;
            string requette = ("SELECT [id_livrable] FROM [dbo].[TB_Livrable] where nom_livrable='"
                        + (nameLivrable + "'"));
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.Read, out DataTable dt);

                idLivrable = dt.Rows.Count == 0 ? 0 : (int)dt.Rows[0]["id_livrable"];

            }
            catch (Exception e)
            {
                ColorConsole.WriteLine(e.ToString(), Color.Red);
                return 0;
            }

            return idLivrable;
        }
        //inserer dossier
        public static bool insertDossier(int iDlivrable, int nbrImages, string nameDossier, string cheminDossier, int userIndex, string user_scan, string date_scan)
        {
            var condition = true;
            string requette = ("INSERT INTO [dbo].[TB_Dossier] ([id_status],[id_livrable],[name_dossier],[url],[nb_image],[date_injec" +
            "t],[user_inject],[date_scan],[user_scan]) VALUES (0,"
                        + (iDlivrable + (",'"
                        + (nameDossier + ("','"
                        + (cheminDossier + ("',"
                        + (nbrImages + (",GETDATE(),"
                        + (userIndex + (",'"
                        + (date_scan + ("','"
                        + (user_scan + "')"))))))))))))));
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.CUD, out DataTable _);

            }
            catch (Exception e)
            {
                condition = false;
                ColorConsole.WriteLine(e.ToString(), Color.Red);
            }

            return condition;
        }
        //inserer livrable
        public static bool insertLivrable(string iduser, string nameLivrable)
        {
            var condition = true;
            string requette = ("INSERT INTO [dbo].[TB_Livrable] ([date_livrable],[user_livrable],[nom_livrable],[etat]) VALUES (GETDA" +
            "TE(),"
                        + (iduser + (",'"
                        + (nameLivrable + "',0)"))));
            try
            {
                new Helper().ExecuteQuery(requette, QueryType.CUD, out DataTable _);

            }
            catch (Exception e)
            {
                condition = false;
                ColorConsole.WriteLine(e.ToString(), Color.Red);
            }

            return condition;
        }

        public static void getPathAndBarCode(string cartonPath ,out Dictionary<int, string> dictioPathAndBarcode)
        {
            dictioPathAndBarcode = new Dictionary<int, string>();
            string pathDocOrder = Path.Combine(cartonPath , "Doc_Order.TXT");
            var lines = File.ReadAllLines(pathDocOrder);
            var i = 0;
            foreach (var piece in lines)
            {
                var path1pg =  Path.Combine(cartonPath ,$"{piece}","1.pg");
                //var pathCommands = Path.Combine(cartonPath, $"{piece}", "COMMANDS");
                var barcode = piece;
                dictioPathAndBarcode.Add(i, $"{barcode};{path1pg}");
            }
        }
        //insertion des pièces
        public static bool insertPieces(int idDossier, Dictionary<int, string> pathAndBarcode, string namedossier)
        {
            var condition = true;
            string requette = "";
            int count = 0;
            for (int i = 0; (i < pathAndBarcode.Count); i++)
            {
                count++;
                var indexes = pathAndBarcode[i].Split(";");
                var chemin = Path.Combine(destination, namedossier, $"{indexes[0]}.tif");

                requette = $"('{indexes[0]}',{idDossier},'{chemin}')";
                var requettefinal = $"INSERT INTO [dbo].[TB_Vues] ([bar_code],[id_dossier],[url]) VALUES {requette}";
                try
                {
                    new Helper().ExecuteQuery(requette, QueryType.CUD, out DataTable _);

                }
                catch (Exception e)
                {
                    condition = false;
                    ColorConsole.WriteLine(e.ToString(), Color.Red);
                }

                requette = "";
            }

            return condition;
        }

        public static bool verifierCarton(string cartonPath)
        {
            var condition = true;
            var carton = new DirectoryInfo(cartonPath);
            var files = carton.GetFiles();
            var FileInfoDoc_Order = false;
            string cheminDocOrder = "";
            foreach (var file in files)
            {
                if (file.Name.Contains("Doc_Order.txt",StringComparison.OrdinalIgnoreCase))
                {
                    FileInfoDoc_Order = true;
                    cheminDocOrder = file.FullName;
                }

            }

            var to = Path.Combine(cheminErreur, Site_Ancfcc, Scan_Ancfcc);
            if (FileInfoDoc_Order)
            {
                
                if (verifierDocOrder(cheminDocOrder))
                {
                    if (!verifierExistanceDesPieces(cheminDocOrder))
                    {
                        to = Path.Combine(to, "PieceNonExistant");
                        condition = false;
                    }

                }
                else
                {
                    to = Path.Combine(to, "DossierFileInfoNonExistant");
                    condition = false;
                }

            }
            else
            {
                to = Path.Combine(to, "AbsenceDocOrder");
                condition = false;
            }
            deplacerCartonErreur(cartonPath, to);
            return condition;
        }

        public static bool deplacerCartonErreur(string cartonPath, string cheminFolderErreurCarton)
        {
            bool condition = true;
            DirectoryInfo carton = new DirectoryInfo(cartonPath);
            DirectoryInfo folderErreur = new DirectoryInfo(cheminFolderErreurCarton);
            if (!folderErreur.Exists)
            {
                folderErreur.Create();
            }

            DirectoryInfo folderErreurCarton = new DirectoryInfo(Path.Combine(folderErreur.FullName , carton.Name));
            try
            {
                carton.MoveTo( folderErreurCarton.FullName);
            }
            catch (Exception e)
            {
                condition = false;
                ColorConsole.WriteLine(e.ToString(), Color.Red);
            }
            return condition;
        }

        public static bool CopieCarton(string cartonPath, string cheminFolderErreurCarton)
        {
            bool condition = true;
            DirectoryInfo carton = new DirectoryInfo(cartonPath);
            DirectoryInfo folderErreur = new DirectoryInfo(cheminFolderErreurCarton);
            if (!folderErreur.Exists)
            {
                folderErreur.Create();
            }

            DirectoryInfo folderErreurCarton = new DirectoryInfo(Path.Combine(folderErreur.FullName, carton.Name));
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(carton.FullName, folderErreurCarton.FullName);
            }
            catch (Exception e)
            {
                condition = false;
                ColorConsole.WriteLine(e.ToString(), Color.Red);
            }
            return condition;
        }

        //Verrifier l'existance de toutes les pièces depuis Doc_Order.txt
        public static bool verifierDocOrder(string pathDocOrder)
        {
            bool condition = true;
            var listePieceInDossier = File.ReadAllLines(pathDocOrder);
            if (listePieceInDossier.Length > 0)
            {
                foreach (var piece in listePieceInDossier)
                {
                    var piecePath = new DirectoryInfo( Path.Combine(
                        Path.GetDirectoryName(pathDocOrder), piece
                    ));

                    if (!piecePath.Exists)
                    {
                        condition = false;
                        break;
                    }
                }
            }
            else
            {
                condition = false;
            }

            return condition;
        }

        //Verrifier l'existance de toutes les fichiers 1.pg des pièces
        public static bool verifierExistanceDesPieces(string pathDocOrder)
        {
            var condition = true;
            var listePieceInDossier = File.ReadAllLines(pathDocOrder);

            foreach (var piece in listePieceInDossier)
            {
                var pieceImgPath = new DirectoryInfo(Path.Combine(
                        Path.GetDirectoryName(pathDocOrder), piece, "1.pg"
                    ));
                if (!pieceImgPath.Exists)
                {
                    condition = false;
                    break;
                }

            }

            return condition;
        }

        //public static string getBarCode(string pathCommand)
        //{
        //    string barcode = "";
        //    DirectoryInfo toread = new DirectoryInfo(pathCommand);
        //    BufferedReader monFich = new BufferedReader(new FileInfoReader(toread));
        //    string ligne = "";
        //    while ((monFich.readLine() != null))
        //    {
        //        if (!ligne.equalsIgnoreCase(""))
        //        {
        //            StringTokenizer st = new StringTokenizer(ligne);
        //            string name = st.nextToken();
        //            if (name.equalsIgnoreCase("BARCODE"))
        //            {
        //                barcode = ligne.substring(name.length()).trim();
        //            }

        //        }

        //    }

        //    monFich.close();
        //    return barcode;
        //}
    }
}
