namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Menu")]
    public partial class Menu
    {
        [Key]
        public int ID_Menu { get; set; }

        [Required]
        [StringLength(250)]
        public string NomeMenu { get; set; }

        [StringLength(255)]
        public string DescrizioneMenu { get; set; }

        [StringLength(255)]
        public string Percorso { get; set; }

        [StringLength(100)]
        public string Controller { get; set; }

        [StringLength(100)]
        public string Azione { get; set; }

        [StringLength(100)]
        public string CategoriaMenu { get; set; }

        [StringLength(100)]
        public string CategoriaMenu2 { get; set; }

        [StringLength(100)]
        public string Icona { get; set; }

        [Required]
        [StringLength(100)]
        public string RuoloPredefinito { get; set; }

        [StringLength(2)]
        public string VoceSingola { get; set; }

        public int Ordine { get; set; }

        [StringLength(2)]
        public string ÈValido { get; set; }

        [Required]
        [StringLength(2)]
        public string MostraNelMenu { get; set; }

        public int? ID_Azienda { get; set; }

        [StringLength(2)]
        public string AccessoRiservato { get; set; }

        [StringLength(2)]
        public string PermessoLettura { get; set; }

        [StringLength(2)]
        public string PermessoAggiunta { get; set; }

        [StringLength(2)]
        public string PermessoModifica { get; set; }

        [StringLength(2)]
        public string PermessoEliminazione { get; set; }

        public DateTime? DataCreazione { get; set; }

        public DateTime? UltimaModifica { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }
    }
}
