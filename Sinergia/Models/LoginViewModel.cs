using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Il nome utente è obbligatorio.")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "La password è obbligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

    }
}