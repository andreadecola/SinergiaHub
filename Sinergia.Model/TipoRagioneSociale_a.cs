namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class TipoRagioneSociale_a
    {
        [Key]
        public int ID_Archivio { get; set; }

        public int ID_TipoRagioneSociale { get; set; }

        [Required]
        [StringLength(50)]
        public string NomeTipo { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        [Required]
        [StringLength(15)]
        public string Stato { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
