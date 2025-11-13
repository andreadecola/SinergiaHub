namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class DocumentiProfessionisti_a
    {
        [Key]
        public int ID_DocumentoArchivio { get; set; }

        public int ID_DocumentoOriginale { get; set; }

        public int ID_Professionista { get; set; }

        [Required]
        [StringLength(255)]
        public string NomeDocumento { get; set; }

        public byte[] FileContent { get; set; }

        [StringLength(255)]
        public string TipoMime { get; set; }

        public DateTime DataCaricamento { get; set; }

        public int ID_UtenteCaricamento { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
