namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class OperatoriSinergia_a
    {
        [Key]
        public int ID_Cliente { get; set; }

        public int? ID_ClienteOriginale { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoCliente { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(100)]
        public string Cognome { get; set; }

        [StringLength(16)]
        public string CodiceFiscale { get; set; }

        [StringLength(20)]
        public string PIVA { get; set; }

        [StringLength(50)]
        public string CodiceUnivoco { get; set; }

        [StringLength(255)]
        public string Indirizzo { get; set; }

        public int? ID_Citta { get; set; }

        public int? ID_Nazione { get; set; }

        [StringLength(20)]
        public string Telefono { get; set; }

        [StringLength(100)]
        public string MAIL1 { get; set; }

        [StringLength(100)]
        public string MAIL2 { get; set; }

        [StringLength(255)]
        public string SitoWEB { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataCreazione { get; set; }

        public DateTime? UltimaModifica { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        [Column(TypeName = "text")]
        public string DescrizioneAttivita { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public byte[] Documento { get; set; }

        [StringLength(100)]
        public string TipoRagioneSociale { get; set; }

        public int? ID_Professione { get; set; }

        public bool? ECliente { get; set; }

        public bool? EFornitore { get; set; }

        [StringLength(50)]
        public string TipoProfessionista { get; set; }

        public bool? PuòGestirePermessi { get; set; }

        public int? ID_UtenteCollegato { get; set; }

        public int? ID_Owner { get; set; }

        public int? ID_SettoreFornitore { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
