using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace Sinergia.Model
{
    public partial class SinergiaDB : DbContext
    {
        public SinergiaDB()
            : base("name=SinergiaDB")
        {
        }

        public virtual DbSet<AnagraficaCostiPratica> AnagraficaCostiPratica { get; set; }
        public virtual DbSet<AnagraficaCostiPratica_a> AnagraficaCostiPratica_a { get; set; }
        public virtual DbSet<AnagraficaCostiProfessionista> AnagraficaCostiProfessionista { get; set; }
        public virtual DbSet<AnagraficaCostiProfessionista_a> AnagraficaCostiProfessionista_a { get; set; }
        public virtual DbSet<AnagraficaCostiTeam> AnagraficaCostiTeam { get; set; }
        public virtual DbSet<AnagraficaCostiTeam_a> AnagraficaCostiTeam_a { get; set; }
        public virtual DbSet<AvvisiParcella> AvvisiParcella { get; set; }
        public virtual DbSet<AvvisiParcella_a> AvvisiParcella_a { get; set; }
        public virtual DbSet<BilancioProfessionista> BilancioProfessionista { get; set; }
        public virtual DbSet<Citta> Citta { get; set; }
        public virtual DbSet<Clienti> Clienti { get; set; }
        public virtual DbSet<Clienti_a> Clienti_a { get; set; }
        public virtual DbSet<Cluster> Cluster { get; set; }
        public virtual DbSet<Cluster_a> Cluster_a { get; set; }
        public virtual DbSet<CompensiPratica> CompensiPratica { get; set; }
        public virtual DbSet<CompensiPratica_a> CompensiPratica_a { get; set; }
        public virtual DbSet<CostiGeneraliUtente> CostiGeneraliUtente { get; set; }
        public virtual DbSet<CostiGeneraliUtente_a> CostiGeneraliUtente_a { get; set; }
        public virtual DbSet<CostiPersonaliUtente> CostiPersonaliUtente { get; set; }
        public virtual DbSet<CostiPersonaliUtente_a> CostiPersonaliUtente_a { get; set; }
        public virtual DbSet<CostiPratica> CostiPratica { get; set; }
        public virtual DbSet<CostiPratica_a> CostiPratica_a { get; set; }
        public virtual DbSet<DatiBancari> DatiBancari { get; set; }
        public virtual DbSet<DatiBancari_a> DatiBancari_a { get; set; }
        public virtual DbSet<DistribuzioneCostiTeam> DistribuzioneCostiTeam { get; set; }
        public virtual DbSet<DistribuzioneCostiTeam_a> DistribuzioneCostiTeam_a { get; set; }
        public virtual DbSet<DocumentiAziende> DocumentiAziende { get; set; }
        public virtual DbSet<DocumentiAziende_a> DocumentiAziende_a { get; set; }
        public virtual DbSet<DocumentiPratiche> DocumentiPratiche { get; set; }
        public virtual DbSet<DocumentiPratiche_a> DocumentiPratiche_a { get; set; }
        public virtual DbSet<EccezioniRicorrenzeCosti> EccezioniRicorrenzeCosti { get; set; }
        public virtual DbSet<EccezioniRicorrenzeCosti_a> EccezioniRicorrenzeCosti_a { get; set; }
        public virtual DbSet<Economico> Economico { get; set; }
        public virtual DbSet<Economico_a> Economico_a { get; set; }
        public virtual DbSet<FinanziamentiProfessionisti> FinanziamentiProfessionisti { get; set; }
        public virtual DbSet<FinanziamentiProfessionisti_a> FinanziamentiProfessionisti_a { get; set; }
        public virtual DbSet<Finanziario> Finanziario { get; set; }
        public virtual DbSet<Finanziario_a> Finanziario_a { get; set; }
        public virtual DbSet<GenerazioneCosti> GenerazioneCosti { get; set; }
        public virtual DbSet<Incassi> Incassi { get; set; }
        public virtual DbSet<Incassi_a> Incassi_a { get; set; }
        public virtual DbSet<KnowledgeAziendale> KnowledgeAziendale { get; set; }
        public virtual DbSet<KnowledgeAziendaleLetture> KnowledgeAziendaleLetture { get; set; }
        public virtual DbSet<LogOperazioniSistema> LogOperazioniSistema { get; set; }
        public virtual DbSet<MembriTeam> MembriTeam { get; set; }
        public virtual DbSet<MembriTeam_a> MembriTeam_a { get; set; }
        public virtual DbSet<Menu> Menu { get; set; }
        public virtual DbSet<MovimentiBancari> MovimentiBancari { get; set; }
        public virtual DbSet<MovimentiBancari_a> MovimentiBancari_a { get; set; }
        public virtual DbSet<Nazioni> Nazioni { get; set; }
        public virtual DbSet<Notifiche> Notifiche { get; set; }
        public virtual DbSet<OperatoriSinergia> OperatoriSinergia { get; set; }
        public virtual DbSet<OperatoriSinergia_a> OperatoriSinergia_a { get; set; }
        public virtual DbSet<OrdiniFornitori> OrdiniFornitori { get; set; }
        public virtual DbSet<OrdiniFornitori_a> OrdiniFornitori_a { get; set; }
        public virtual DbSet<Permessi> Permessi { get; set; }
        public virtual DbSet<Permessi_a> Permessi_a { get; set; }
        public virtual DbSet<PermessiDelegabiliPerProfessionista> PermessiDelegabiliPerProfessionista { get; set; }
        public virtual DbSet<PermessiDelegabiliPerProfessionista_a> PermessiDelegabiliPerProfessionista_a { get; set; }
        public virtual DbSet<PlafondUtente> PlafondUtente { get; set; }
        public virtual DbSet<PlafondUtente_a> PlafondUtente_a { get; set; }
        public virtual DbSet<Pratiche> Pratiche { get; set; }
        public virtual DbSet<Pratiche_a> Pratiche_a { get; set; }
        public virtual DbSet<Previsione> Previsione { get; set; }
        public virtual DbSet<Previsione_a> Previsione_a { get; set; }
        public virtual DbSet<Professioni> Professioni { get; set; }
        public virtual DbSet<Professioni_a> Professioni_a { get; set; }
        public virtual DbSet<RelazionePraticheUtenti> RelazionePraticheUtenti { get; set; }
        public virtual DbSet<RelazionePraticheUtenti_a> RelazionePraticheUtenti_a { get; set; }
        public virtual DbSet<RelazioneUtenti> RelazioneUtenti { get; set; }
        public virtual DbSet<RelazioneUtenti_a> RelazioneUtenti_a { get; set; }
        public virtual DbSet<RicorrenzeCosti> RicorrenzeCosti { get; set; }
        public virtual DbSet<RicorrenzeCosti_a> RicorrenzeCosti_a { get; set; }
        public virtual DbSet<RimborsiPratica> RimborsiPratica { get; set; }
        public virtual DbSet<RimborsiPratica_a> RimborsiPratica_a { get; set; }
        public virtual DbSet<RuoliPratiche> RuoliPratiche { get; set; }
        public virtual DbSet<SettoriFornitori> SettoriFornitori { get; set; }
        public virtual DbSet<SettoriFornitori_a> SettoriFornitori_a { get; set; }
        public virtual DbSet<TeamProfessionisti> TeamProfessionisti { get; set; }
        public virtual DbSet<TeamProfessionisti_a> TeamProfessionisti_a { get; set; }
        public virtual DbSet<TemplateIncarichi> TemplateIncarichi { get; set; }
        public virtual DbSet<TemplateIncarichi_a> TemplateIncarichi_a { get; set; }
        public virtual DbSet<TipologieCosti> TipologieCosti { get; set; }
        public virtual DbSet<TipologieCosti_a> TipologieCosti_a { get; set; }
        public virtual DbSet<TipoRagioneSociale> TipoRagioneSociale { get; set; }
        public virtual DbSet<TipoRagioneSociale_a> TipoRagioneSociale_a { get; set; }
        public virtual DbSet<Utenti> Utenti { get; set; }
        public virtual DbSet<Utenti_a> Utenti_a { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AnagraficaCostiTeam>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AnagraficaCostiTeam_a>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AvvisiParcella>()
                .Property(e => e.ContributoIntegrativoPercentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<AvvisiParcella>()
                .Property(e => e.ContributoIntegrativoImporto)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AvvisiParcella>()
                .Property(e => e.AliquotaIVA)
                .HasPrecision(5, 2);

            modelBuilder.Entity<AvvisiParcella_a>()
                .Property(e => e.ContributoIntegrativoPercentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<AvvisiParcella_a>()
                .Property(e => e.ContributoIntegrativoImporto)
                .HasPrecision(10, 2);

            modelBuilder.Entity<AvvisiParcella_a>()
                .Property(e => e.AliquotaIVA)
                .HasPrecision(5, 2);

            modelBuilder.Entity<BilancioProfessionista>()
                .Property(e => e.TipoVoce)
                .IsUnicode(false);

            modelBuilder.Entity<BilancioProfessionista>()
                .Property(e => e.Categoria)
                .IsUnicode(false);

            modelBuilder.Entity<BilancioProfessionista>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<BilancioProfessionista>()
                .Property(e => e.Origine)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CODREG)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CODPRV)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CODCOM)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CODFIN)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CAP)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.SGLPRV)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.CODUSL)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.Regione)
                .IsUnicode(false);

            modelBuilder.Entity<Citta>()
                .Property(e => e.SiglaNazione)
                .IsUnicode(false);

            modelBuilder.Entity<Clienti>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Clienti_a>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Cluster>()
                .Property(e => e.TipoCluster)
                .IsUnicode(false);

            modelBuilder.Entity<Cluster>()
                .Property(e => e.PercentualePrevisione)
                .HasPrecision(5, 2);

            modelBuilder.Entity<CostiPersonaliUtente>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<CostiPersonaliUtente_a>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<CostiPratica>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<CostiPratica_a>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<DistribuzioneCostiTeam>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<DistribuzioneCostiTeam_a>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<DistribuzioneCostiTeam_a>()
                .Property(e => e.ModificheTestuali)
                .IsUnicode(false);

            modelBuilder.Entity<Economico>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Economico>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Economico>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Economico_a>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Economico_a>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Economico_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<FinanziamentiProfessionisti>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<FinanziamentiProfessionisti_a>()
                .Property(e => e.Importo)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Finanziario>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Finanziario>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Finanziario>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Finanziario_a>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Finanziario_a>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Finanziario_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<LogOperazioniSistema>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<MembriTeam>()
                .Property(e => e.PercentualeCondivisione)
                .HasPrecision(5, 2);

            modelBuilder.Entity<MembriTeam_a>()
                .Property(e => e.PercentualeCondivisione)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Menu>()
                .Property(e => e.DescrizioneMenu)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.Percorso)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.Controller)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.Azione)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.CategoriaMenu)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.CategoriaMenu2)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.Icona)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.RuoloPredefinito)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.VoceSingola)
                .IsFixedLength()
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.ÈValido)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.MostraNelMenu)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.AccessoRiservato)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.PermessoLettura)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.PermessoAggiunta)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.PermessoModifica)
                .IsUnicode(false);

            modelBuilder.Entity<Menu>()
                .Property(e => e.PermessoEliminazione)
                .IsUnicode(false);

            modelBuilder.Entity<Nazioni>()
                .Property(e => e.NameNazione)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.TipoCliente)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Nome)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Cognome)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.CodiceFiscale)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.PIVA)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.CodiceUnivoco)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Indirizzo)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Telefono)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.MAIL1)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.MAIL2)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.SitoWEB)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.DescrizioneAttivita)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.TipoCliente)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Nome)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Cognome)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.CodiceFiscale)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.PIVA)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.CodiceUnivoco)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Indirizzo)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Telefono)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.MAIL1)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.MAIL2)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.SitoWEB)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.DescrizioneAttivita)
                .IsUnicode(false);

            modelBuilder.Entity<OperatoriSinergia_a>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori>()
                .Property(e => e.Importo)
                .HasPrecision(12, 2);

            modelBuilder.Entity<OrdiniFornitori>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori_a>()
                .Property(e => e.Importo)
                .HasPrecision(12, 2);

            modelBuilder.Entity<OrdiniFornitori_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori_a>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<OrdiniFornitori_a>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Permessi>()
                .Property(e => e.Studio)
                .IsUnicode(false);

            modelBuilder.Entity<Permessi>()
                .Property(e => e.Abilitato)
                .IsUnicode(false);

            modelBuilder.Entity<Permessi_a>()
                .Property(e => e.Studio)
                .IsUnicode(false);

            modelBuilder.Entity<Permessi_a>()
                .Property(e => e.Abilitato)
                .IsUnicode(false);

            modelBuilder.Entity<PlafondUtente>()
                .Property(e => e.ImportoTotale)
                .HasPrecision(10, 2);

            modelBuilder.Entity<PlafondUtente_a>()
                .Property(e => e.ImportoTotale)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Titolo)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Budget)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.Tipologia)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.TerminiPagamento)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.GradoGiudizio)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.OrePreviste)
                .HasPrecision(8, 2);

            modelBuilder.Entity<Pratiche>()
                .Property(e => e.OreEffettive)
                .HasPrecision(8, 2);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Titolo)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Budget)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.Tipologia)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.TerminiPagamento)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.GradoGiudizio)
                .IsUnicode(false);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.OrePreviste)
                .HasPrecision(8, 2);

            modelBuilder.Entity<Pratiche_a>()
                .Property(e => e.OreEffettive)
                .HasPrecision(8, 2);

            modelBuilder.Entity<Previsione>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Previsione>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Previsione>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Previsione_a>()
                .Property(e => e.Percentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Previsione_a>()
                .Property(e => e.TipoOperazione)
                .IsUnicode(false);

            modelBuilder.Entity<Previsione_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Professioni>()
                .Property(e => e.PercentualeContributoIntegrativo)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Professioni_a>()
                .Property(e => e.PercentualeContributoIntegrativo)
                .HasPrecision(5, 2);

            modelBuilder.Entity<RelazionePraticheUtenti>()
                .Property(e => e.Ruolo)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti>()
                .Property(e => e.TipoRelazione)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti_a>()
                .Property(e => e.TipoRelazione)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<RelazioneUtenti_a>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<RicorrenzeCosti>()
                .Property(e => e.Valore)
                .HasPrecision(10, 2);

            modelBuilder.Entity<RicorrenzeCosti_a>()
                .Property(e => e.Valore)
                .HasPrecision(10, 2);

            modelBuilder.Entity<RuoliPratiche>()
                .Property(e => e.NomeRuolo)
                .IsUnicode(false);

            modelBuilder.Entity<TipologieCosti>()
                .Property(e => e.ValorePercentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<TipologieCosti>()
                .Property(e => e.ValoreFisso)
                .HasPrecision(10, 2);

            modelBuilder.Entity<TipologieCosti_a>()
                .Property(e => e.ValorePercentuale)
                .HasPrecision(5, 2);

            modelBuilder.Entity<TipologieCosti_a>()
                .Property(e => e.ValoreFisso)
                .HasPrecision(10, 2);

            modelBuilder.Entity<TipoRagioneSociale>()
                .Property(e => e.NomeTipo)
                .IsUnicode(false);

            modelBuilder.Entity<TipoRagioneSociale>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<TipoRagioneSociale>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<TipoRagioneSociale_a>()
                .Property(e => e.NomeTipo)
                .IsUnicode(false);

            modelBuilder.Entity<TipoRagioneSociale_a>()
                .Property(e => e.Descrizione)
                .IsUnicode(false);

            modelBuilder.Entity<TipoRagioneSociale_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.TipoUtente)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Nome)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Cognome)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.CodiceFiscale)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.PIVA)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.CodiceUnivoco)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Telefono)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Cellulare1)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Cellulare2)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.MAIL1)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.MAIL2)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.SitoWEB)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.PasswordHash)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Salt)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.DescrizioneAttivita)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti>()
                .Property(e => e.Note)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.TipoUtente)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Nome)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Cognome)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.CodiceFiscale)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.PIVA)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.CodiceUnivoco)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Telefono)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Cellulare1)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Cellulare2)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.MAIL1)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.MAIL2)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.SitoWEB)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Stato)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.PasswordHash)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Salt)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.DescrizioneAttivita)
                .IsUnicode(false);

            modelBuilder.Entity<Utenti_a>()
                .Property(e => e.Note)
                .IsUnicode(false);
        }
    }
}
