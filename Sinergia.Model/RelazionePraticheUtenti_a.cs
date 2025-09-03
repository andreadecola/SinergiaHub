namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class RelazionePraticheUtenti_a
    {
        [Key]
        public int ID_Relazione_a { get; set; }

        public int? ID_Relazione_Originale { get; set; }

        public int ID_Pratiche { get; set; }

        public int ID_Utente { get; set; }

        [Required]
        [StringLength(50)]
        public string Ruolo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataAssegnazione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
