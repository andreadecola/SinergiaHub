namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class TeamProfessionisti_a
    {
        [Key]
        public int ID_VersioneTeam { get; set; }

        public int ID_Team { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        public bool Attivo { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
