namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Pratiche")]
    public partial class Pratiche
    {
        [Key]
        public int ID_Pratiche { get; set; }

        [Required]
        [StringLength(255)]
        public string Titolo { get; set; }

        [Column(TypeName = "text")]
        public string Descrizione { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizioAttivitaStimata { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFineAttivitaStimata { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_UtenteResponsabile { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public decimal Budget { get; set; }

        public DateTime? DataCreazione { get; set; }

        public DateTime? UltimaModifica { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public int? ID_Pratica_Originale { get; set; }

        public int? ID_Owner { get; set; }

        [StringLength(50)]
        public string Tipologia { get; set; }

        public decimal? ImportoFisso { get; set; }

        [Column(TypeName = "text")]
        public string TerminiPagamento { get; set; }

        public decimal? TariffaOraria { get; set; }

        [StringLength(50)]
        public string GradoGiudizio { get; set; }

        public decimal? AccontoGiudiziale { get; set; }

        public decimal? OrePreviste { get; set; }

        public decimal? OreEffettive { get; set; }

        public decimal? TrattenutaPersonalizzata { get; set; }

        public bool HaIncaricoGenerato { get; set; }
    }
}
