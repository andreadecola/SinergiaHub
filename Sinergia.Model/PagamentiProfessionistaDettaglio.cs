namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("PagamentiProfessionistaDettaglio")]
    public partial class PagamentiProfessionistaDettaglio
    {
        [Key]
        public int ID_PagamentoDettaglio { get; set; }

        public int ID_Pagamento { get; set; }

        public int ID_BilancioProfessionista { get; set; }

        public decimal ImportoPagato { get; set; }

        public DateTime DataInserimento { get; set; }

        public int ID_UtenteInserimento { get; set; }
    }
}
