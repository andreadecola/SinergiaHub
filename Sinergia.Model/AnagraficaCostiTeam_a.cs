namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class AnagraficaCostiTeam_a
    {
        [Key]
        public int IDVersioneAnagraficaCostoTeam { get; set; }

        public int? ID_AnagraficaCostoTeam { get; set; }

        public int? ID_Professione { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        public bool? Ricorrente { get; set; }

        public int? NumeroVersione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }

        [Required]
        [StringLength(50)]
        public string Stato { get; set; }
    }
}
