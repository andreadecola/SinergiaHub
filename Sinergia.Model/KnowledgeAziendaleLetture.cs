namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("KnowledgeAziendaleLetture")]
    public partial class KnowledgeAziendaleLetture
    {
        [Key]
        public int LetturaID { get; set; }

        public int KnowledgeAziendaleID { get; set; }

        public int ID_Utente { get; set; }

        public DateTime DataLettura { get; set; }
    }
}
