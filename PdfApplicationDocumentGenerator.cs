﻿using Nml.Improve.Me.Dependencies;
using System;
using System.Linq;

namespace Nml.Improve.Me
{
    public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
    {
        private readonly IDataContext _dataContext;
        private IPathProvider _templatePathProvider;
        public IViewGenerator _viewGenerator;
        internal readonly IConfiguration _configuration;
        private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
        private readonly IPdfGenerator _pdfGenerator;

        public PdfApplicationDocumentGenerator(
            IDataContext dataContext,
            IPathProvider templatePathProvider,
            IViewGenerator viewGenerator,
            IConfiguration configuration,
            IPdfGenerator pdfGenerator,
            ILogger<PdfApplicationDocumentGenerator> logger)
        {
        
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
            _viewGenerator = viewGenerator;
            _configuration = configuration;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pdfGenerator = pdfGenerator;
        }

        public byte[] Generate(Guid applicationId, string baseUri)
        {
            string view ="";
            string path = "";

            Application application = _dataContext.Applications.Single(app => app.Id == applicationId);
            var inReviewMessage = "Your application has been placed in review" +
                                        application.CurrentReview.Reason switch
                                        {
                                            { } reason when reason.Contains("address") =>
                                                " pending outstanding address verification for FICA purposes.",
                                            { } reason when reason.Contains("bank") =>
                                                " pending outstanding bank account verification.",
                                            _ =>
                                                " because of suspicious account behaviour. Please contact support ASAP."
                                        };
            if (application != null)
            {

                if (baseUri.EndsWith("/"))
                    baseUri = baseUri.Substring(baseUri.Length - 1);

                switch (application.State)
                {
                    case ApplicationState.Pending:

                        path = _templatePathProvider.Get("PendingApplication");
                        PendingApplicationViewModel vm = new PendingApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = application.Person.FirstName + " " + application.Person.Surname,
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };

                        view = _viewGenerator.GenerateFromPath(baseUri + path, vm);
                        break;
                    case ApplicationState.Activated:
                        path = _templatePathProvider.Get("ActivatedApplication");
                        ActivatedApplicationViewModel am = new ActivatedApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                            PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                                       .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                                       .Sum(),
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };

                        view = _viewGenerator.GenerateFromPath(baseUri + path, am);
                        break;
                    case ApplicationState.InReview:

                        path = _templatePathProvider.Get("InReviewApplication");
                        InReviewApplicationViewModel rm = new InReviewApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                            PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                            .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                            .Sum(),
                            InReviewMessage = inReviewMessage,
                            InReviewInformation = application.CurrentReview,
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };
                        view = _viewGenerator.GenerateFromPath(baseUri + path, rm);
                        break;
                    case ApplicationState.Closed:
                        path = _templatePathProvider.Get("ClosedApplication");
                        ClosedApplicationViewModel cm = new ClosedApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                            PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                                      .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                                      .Sum(),
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };
                        view = _viewGenerator.GenerateFromPath(baseUri + path, cm);
                        break;
                    default:
                        _logger.LogWarning(
                        $"The application is in state '{application.State}' and no valid document can be generated for it.");

                        break;

                };

                

                var pdfOptions = new PdfOptions
                {
                    PageNumbers = PageNumbers.Numeric,
                    HeaderOptions = new HeaderOptions
                    {
                        HeaderRepeat = HeaderRepeat.FirstPageOnly,
                        HeaderHtml = PdfConstants.Header
                    }
                };
                var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
                return pdf.ToBytes();
            }
            else
            {

                _logger.LogWarning(
                    $"No application found for id '{applicationId}'");
                return null;
            }
        }
    }
}
