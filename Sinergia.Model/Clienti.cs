namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Clienti")]
    public partial class Clienti
    {
        [Key]
        public int ID_Cliente { get; set; }

        [Required]
        [StringLength(255)]
        public string Nome { get; set; }

        [StringLength(255)]
        public string Cognome { get; set; }

        [StringLength(255)]
        public string RagioneSociale { get; set; }

        [StringLength(16)]
        public string CodiceFiscale { get; set; }

        [StringLength(20)]
        public string PIVA { get; set; }

        [StringLength(255)]
        public string Indirizzo { get; set; }

        public int? ID_Citta { get; set; }

        public int? ID_Nazione { get; set; }

        [StringLength(20)]
        public string Telefono { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        [StringLength(50)]
        public string TipoCliente { get; set; }

        public DateTime? DataCreazione { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int ID_Operatore { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoOperatore { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }
    }
}
