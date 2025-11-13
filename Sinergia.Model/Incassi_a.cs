namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Incassi_a
    {
        [Key]
        public int ID_Archivio { get; set; }

        public int? ID_IncassoOriginale { get; set; }

        public int? ID_Pratiche { get; set; }

        public DateTime? DataIncasso { get; set; }

        public decimal? Importo { get; set; }

        [StringLength(50)]
        public string ModalitaPagamento { get; set; }

        public decimal? Utile { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        public bool? VersaInPlafond { get; set; }

        public int? ID_AvvisoParcella { get; set; }

        public decimal? ImportoTotale { get; set; }

        public decimal? ImportoNetto { get; set; }

        public decimal? ImportoVersatoPlafond { get; set; }

        [StringLength(50)]
        public string StatoIncasso { get; set; }

        [StringLength(255)]
        public string Note { get; set; }

        public DateTime? DataCompetenzaEconomica { get; set; }

        public DateTime? DataCompetenzaFinanziaria { get; set; }

        public int? ID_Responsabile { get; set; }

        public decimal? PercentualeResponsabile { get; set; }

        public decimal? ImportoResponsabile { get; set; }

        public int? ID_OwnerCliente { get; set; }

        public decimal? PercentualeOwner { get; set; }

        public decimal? ImportoOwner { get; set; }

        [StringLength(255)]
        public string ID_Collaboratori { get; set; }

        [StringLength(255)]
        public string PercentualiCollaboratori { get; set; }

        [StringLength(255)]
        public string ImportiCollaboratori { get; set; }

        [StringLength(20)]
        public string TipoMovimento { get; set; }

        [StringLength(50)]
        public string Categoria { get; set; }
    }
}
