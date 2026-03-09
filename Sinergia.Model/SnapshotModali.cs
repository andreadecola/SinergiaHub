namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("SnapshotModali")]
    public partial class SnapshotModali
    {
        [Key]
        public int ID_Snapshot { get; set; }

        [StringLength(20)]
        public string Tipo { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_AvvisoParcella { get; set; }

        public int? ID_Incasso { get; set; }

        public string HtmlSnapshot { get; set; }

        public DateTime? DataCreazione { get; set; }
    }
}
