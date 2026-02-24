namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("PagamentiProfessionista")]
    public partial class PagamentiProfessionista
    {
        [Key]
        public int ID_Pagamento { get; set; }

        public int ID_Professionista { get; set; }

        public int ID_Operatore { get; set; }

        public decimal ImportoTotale { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataPagamento { get; set; }

        [Required]
        [StringLength(100)]
        public string ModalitaPagamento { get; set; }

        [Required]
        [StringLength(200)]
        public string RiferimentoPagamento { get; set; }

        [Required]
        [StringLength(500)]
        public string Note { get; set; }

        [Required]
        [StringLength(50)]
        public string Stato { get; set; }

        public DateTime DataInserimento { get; set; }

        public int ID_UtenteInserimento { get; set; }

        public DateTime DataUltimaModifica { get; set; }

        public int ID_UtenteUltimaModifica { get; set; }
    }
}
