namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class CompensiPraticaDettaglio_a
    {
        [Key]
        public int ID_RigaCompenso_a { get; set; }

        public int ID_RigaCompensoOriginale { get; set; }

        public int ID_Pratiche { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoCompenso { get; set; }

        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        [StringLength(50)]
        public string Categoria { get; set; }

        public decimal? ValoreStimato { get; set; }

        public int Ordine { get; set; }

        public string EstremiGiudizio { get; set; }

        public string OggettoIncarico { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? UltimaModifica { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }

        public int? ID_ProfessionistaIntestatario { get; set; }

        public string Collaboratori { get; set; }

        [StringLength(255)]
        public string FaseGiudiziale { get; set; }

        public decimal? ImportoInviatoAllaFatturazione { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal? ImportoResiduo { get; set; }

        public DateTime? DataCompetenzaEconomica { get; set; }

        public int? ID_AvvisoParcella { get; set; }
    }
}
