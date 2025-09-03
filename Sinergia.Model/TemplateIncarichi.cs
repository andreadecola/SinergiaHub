namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("TemplateIncarichi")]
    public partial class TemplateIncarichi
    {
        [Key]
        public int IDTemplateIncarichi { get; set; }

        [StringLength(100)]
        public string NomeTemplate { get; set; }

        public string ContenutoHtml { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int ID_Professione { get; set; }
    }
}
