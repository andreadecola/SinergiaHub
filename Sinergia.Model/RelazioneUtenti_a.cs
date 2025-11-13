namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class RelazioneUtenti_a
    {
        [Key]
        public int ID_Relazione { get; set; }

        public int? ID_Utente { get; set; }

        public int ID_UtenteAssociato { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoRelazione { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        [StringLength(15)]
        public string Stato { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        public int? ID_Team { get; set; }
    }
}
