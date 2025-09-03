namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Utenti_a
    {
        [Key]
        public int IDVersioneUtenti { get; set; }

        public int ID_Utente { get; set; }

        public int ID_UtenteOriginale { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoUtente { get; set; }

        [Required]
        [StringLength(50)]
        public string Nome { get; set; }

        [Required]
        [StringLength(50)]
        public string Cognome { get; set; }

        [StringLength(16)]
        public string CodiceFiscale { get; set; }

        [StringLength(20)]
        public string PIVA { get; set; }

        [StringLength(50)]
        public string CodiceUnivoco { get; set; }

        public int? ID_CittaResidenza { get; set; }

        public int? ID_Nazione { get; set; }

        [StringLength(20)]
        public string Telefono { get; set; }

        [StringLength(20)]
        public string Cellulare1 { get; set; }

        [StringLength(20)]
        public string Cellulare2 { get; set; }

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

        [StringLength(255)]
        public string PasswordHash { get; set; }

        [StringLength(255)]
        public string Salt { get; set; }

        [Column(TypeName = "text")]
        public string DescrizioneAttivita { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public byte[] DOCUMENTO { get; set; }

        [StringLength(250)]
        public string Indirizzo { get; set; }

        [StringLength(50)]
        public string PasswordTemporanea { get; set; }

        [StringLength(250)]
        public string NomeAccount { get; set; }

        [StringLength(50)]
        public string Ruolo { get; set; }

        [StringLength(255)]
        public string FotoProfiloPath { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
