namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PagamentiProfessionista_a
    {
        [Key]
        public int ID_PagamentoArchivio { get; set; }

        public int ID_PagamentoOriginale { get; set; }

        public int ID_Professionista { get; set; }

        public int ID_Operatore { get; set; }

        public decimal ImportoTotale { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataPagamento { get; set; }

        [Required]
        [StringLength(100)]
        public string ModalitaPagamento { get; set; }

        [Required]
        [StringLength(500)]
        public string Note { get; set; }

        [Required]
        [StringLength(50)]
        public string Stato { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }
    }
}
