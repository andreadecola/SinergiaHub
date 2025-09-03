namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("LogOperazioniSistema")]
    public partial class LogOperazioniSistema
    {
        [Key]
        public int ID_Log { get; set; }

        [StringLength(100)]
        public string NomeOperazione { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataEsecuzione { get; set; }

        public string Note { get; set; }

        [Column(TypeName = "text")]
        public string Descrizione { get; set; }
    }
}
