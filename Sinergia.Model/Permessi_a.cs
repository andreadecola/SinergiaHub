namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Permessi_a
    {
        [Key]
        public int ID_Permesso { get; set; }

        public int ID_Utente { get; set; }

        public int ID_Menu { get; set; }

        [StringLength(50)]
        public string Studio { get; set; }

        [StringLength(3)]
        public string Abilitato { get; set; }

        public bool? Vedi { get; set; }

        public bool? Aggiungi { get; set; }

        public bool? Modifica { get; set; }

        public bool? Elimina { get; set; }

        public DateTime? DataAssegnazione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
