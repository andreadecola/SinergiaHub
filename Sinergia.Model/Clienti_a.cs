namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Clienti_a
    {
        [Key]
        public int ID_Cliente_a { get; set; }

        public int ID_Cliente_Originale { get; set; }

        [StringLength(255)]
        public string Nome { get; set; }

        [StringLength(255)]
        public string Cognome { get; set; }

        [StringLength(255)]
        public string RagioneSociale { get; set; }

        [StringLength(255)]
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

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        [StringLength(255)]
        public string DocumentoCliente_Nome { get; set; }

        public byte[] DocumentoCliente_File { get; set; }
    }
}
