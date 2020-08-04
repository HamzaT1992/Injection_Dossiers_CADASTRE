using System;
using System.Collections.Generic;
using System.Text;

namespace Injection_Dossiers_CADASTRE
{
    public class Site
    {
        public int id { get; set; }
        public string Nom { get; set; }
        public string DB { get; set; }
        public string chemin_source { get; set; }
        public string chemin_erreur { get; set; }
        public string chemin_destination { get; set; }
        public string chemin_archive_source { get; set; }
        public string chemin_archive_destination { get; set; }

    }
}
