using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Linq;
using static System.Console;
using ColorConsole = Colorful.Console;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using Newtonsoft.Json.Linq;
using System.IO;
using Colorful;

namespace Injection_Dossiers_CADASTRE
{

    public enum QueryType
    {
        //select query
        Read,
        //insert, update, delete
        CUD
    }
    public class Helper
    {
        public static IConfiguration config { get; set; }
        public List<Site> Sites { get; set; }

        public static Site selectedSite;
        public Helper()
        {  
            
            config = new ConfigurationBuilder()
                                          .AddJsonFile("appsettings.json", true, true)
                                          .Build();

            var json = JObject.Parse(File.ReadAllText("sites.json"));
            Sites = json["Sites"].ToObject<List<Site>>();
            

        }

        public void Menu()
        {
            var selected = false;
            ColorAlternatorFactory alternatorFactory = new ColorAlternatorFactory();
            ColorAlternator alternator = alternatorFactory.GetAlternator(1, Color.Plum, Color.PaleVioletRed);
            do
            {
                Clear();
                WriteLine("Choisir un site :");
                foreach(var site in Sites)
                {
                    ColorConsole.WriteLineAlternating($"\t {site.id} - {site.Nom}",alternator);
                }
                WriteLine($"\t 0 - Quitter");
                Write("entrer le numéro : ");
                if (int.TryParse(ReadLine(), out int selectedValue))
                {
                    if (selectedValue == 0)
                    {
                        Environment.Exit(0);
                    }

                    selectedSite = Sites.FirstOrDefault(s => s.id == selectedValue);
                    if (selectedSite == null)
                    {
                        ColorConsole.WriteLine($"Le numéro {selectedValue} est introuvable!!", Color.Red);
                        WriteLine("Merci de d'entrer un numéro du Menu");
                    }
                    else
                    {
                        selected = true;
                        Clear();
                    }
                }
                
            } while (!selected);
        }

        public static void ExecuteQuery(string query,QueryType queryType ,out DataTable dt)
        {
            dt = new DataTable();

            var conStr = config.GetConnectionString("Storage").Replace("@database", selectedSite.DB);
            using (var con = new SqlConnection(conStr))
            {
                con.Open();
                var cmd = new SqlCommand()
                {
                    CommandText = query,
                    CommandTimeout = 0,
                    Connection = con
                };
                switch (queryType)
                {
                    case QueryType.Read:
                        var da = new SqlDataAdapter(cmd);
                        da.Fill(dt);
                        break;
                    case QueryType.CUD:
                        cmd.ExecuteNonQuery();
                        break;
                }
            }
        }
    }
}
