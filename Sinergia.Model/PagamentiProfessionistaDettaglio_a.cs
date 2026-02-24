namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PagamentiProfessionistaDettaglio_a
    {
        [Key]
        public int ID_PagamentoDettaglioArchivio { get; set; }

        public int ID_PagamentoDettaglioOriginale { get; set; }

        public int ID_Pagamento { get; set; }

        public int ID_BilancioProfessionista { get; set; }

        public decimal ImportoPagato { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }
    }
}
