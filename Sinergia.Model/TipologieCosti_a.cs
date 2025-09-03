namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class TipologieCosti_a
    {
        [Key]
        public int ID_Storico { get; set; }

        public int ID_TipoCosto { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        public decimal? ValorePercentuale { get; set; }

        public decimal? ValoreFisso { get; set; }

        [Required]
        [StringLength(50)]
        public string Tipo { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        public DateTime DataInizio { get; set; }

        public DateTime? DataFine { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        [Required]
        [StringLength(20)]
        public string Operazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoCostoApplicazione { get; set; }
    }
}
