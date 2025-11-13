namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class TemplateIncarichi_a
    {
        [Key]
        public int ID_Archivio { get; set; }

        public int? ID_TemplateIncarichiOriginale { get; set; }

        [StringLength(100)]
        public string NomeTemplate { get; set; }

        public string ContenutoHtml { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int ID_Professione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        [StringLength(100)]
        public string TipoCompenso { get; set; }

        public byte[] FileDocx { get; set; }
    }
}
