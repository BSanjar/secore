using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApplication1.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    [ServiceFilter(typeof(WebApplication1.Filters.XmlValidationFilter))]
    public class WebApiController : ControllerBase
    {

        private readonly AppDbContext _db;
        private readonly WebApiAuthService _authService;
       

        public WebApiController(AppDbContext db, WebApiAuthService authService)
        {
            _db = db;
            _authService = authService;
        }


        #region check
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<CheckResponse> check([FromBody] CheckRequest request)
        {
            // Проверка на null
            if (request == null)
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.UnknownRequest);
            }

            // Валидация обязательных полей
            if (string.IsNullOrWhiteSpace(request.Login))
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.LoginNotProvided, request.Account);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.PasswordNotProvided, request.Account);
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.OperatorNotProvided, request.Account);
            }

            if (string.IsNullOrWhiteSpace(request.Account))
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotProvided);
            }

            // Валидация формата account (только цифры)
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Account, @"^\d+$"))
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.InvalidAccount, request.Account);
            }

            // Авторизация
            var organization = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (organization == null)
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AuthenticationFailed, request.Account);
            }

            // Парсинг тела запроса выполняется автоматически через model binding
            // request уже содержит распарсенные данные из XML

            
            List<Invoice> invoices = new List<Invoice>();
            invoices = await _db.Invoices.Where( 
                a=>a.PayCode==request.Account && 
                a.InvoiceStatus=="actual" &&
                a.ClientNavigation.Organization == organization.Id).ToListAsync();

            //есть ли актуальные счета
            if(invoices.Count == 0) 
            {
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotFound, request.Account);
            }
            else
            {
                //тут потом можно сделать проверку есть ли клиент
                OrganizationClient client = await _db.OrganizationClients.FirstOrDefaultAsync(a => a.Id == invoices.First().Client);

                //данные по клиенту
                var additional = new AdditionalInfo
                {                    
                    Items = new List<AdditionalItem>
                {
                    new AdditionalItem { Name = "client_inn", Value = client.ClientInn },
                    new AdditionalItem { Name = "client_adres", Value = client.ClientAdres },
                    new AdditionalItem { Name = "client_phone", Value = client.ClientPhone },
                    new AdditionalItem { Name = "client_email", Value = client.ClientEmail },
                }

                };



                string service = "";
                string nextPayDate = "";
                string fixedSumm = "";
                decimal? fixedSum = 0;
                decimal? recomendedSum = 0;
                if (invoices.Count > 1)
                {
                    //тут просто вывожу наименование счетов слитно. потом можно как то изменить)
                    foreach (var invoice in invoices)
                    {
                        service += invoice.NameInvoice + " ";
                    }

                    //Как рекомендуемую сумму к оплате передаю сумму всех фиксированных сумм=баланс.                   
                    foreach (var invoice in invoices)
                    {
                        if (invoice.FixedSumm > 0)
                        {
                            fixedSum += invoice.FixedSumm;
                        }
                    }

                    if (invoices.First().Balance > 0)
                    {
                        recomendedSum = fixedSum - invoices.First().Balance;
                    }

                    if (fixedSum > 0)
                    {
                        additional.Items.Add(new AdditionalItem { Name = "fixedSum", Value = ParsersHelper.ToMoneyStringFromCents(fixedSum) });

                        if (recomendedSum > 0)
                        {
                            additional.Items.Add(new AdditionalItem { Name = "recomendedSumPay", Value = ParsersHelper.ToMoneyStringFromCents(recomendedSum) });
                        }
                        else
                        {
                            //если нету долгов итд то рекомендуется оплатить ту же сумму
                            additional.Items.Add(new AdditionalItem { Name = "recomendedSumPay", Value = ParsersHelper.ToMoneyStringFromCents(fixedSum) });
                        }
                    }
                }
                else
                {
                    service = invoices.First().NameInvoice;

                    nextPayDate = invoices.First().NextStartInvoice?.ToString("dd.MM.yyyy");
                    additional.Items.Add(new AdditionalItem { Name = "nextPayDate", Value = nextPayDate });

                    fixedSum = invoices.First().FixedSumm;
                    if (fixedSum > 0)
                    {
                        if (invoices.First().Balance > 0)
                        {
                            recomendedSum = fixedSum - invoices.First().Balance;
                        }
                        additional.Items.Add(new AdditionalItem { Name = "fixedSum", Value = ParsersHelper.ToMoneyStringFromCents(fixedSum) });

                        if (recomendedSum > 0)
                        {
                            additional.Items.Add(new AdditionalItem { Name = "recomendedSumPay", Value = ParsersHelper.ToMoneyStringFromCents(recomendedSum) });
                        }
                        else
                        {
                            //если нету долгов итд то рекомендуется оплатить ту же сумму
                            additional.Items.Add(new AdditionalItem { Name = "recomendedSumPay", Value = ParsersHelper.ToMoneyStringFromCents(fixedSum) });
                        }
                    }
                }

                //баланс счета\счетов               
                additional.Items.Add(new AdditionalItem { Name = "balanceSum", Value = ParsersHelper.ToMoneyStringFromCents(invoices?.FirstOrDefault()?.Balance) });


                return WebApiResponseService.CreateCheckSuccessResponse(
                    account: request.Account,
                    walletAccount: "1256982", // потом
                    service: service, 
                    payerName: client.ClientName, 
                    additional: additional
                );
            }          
        }
        #endregion

        #region pay
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PayResponse> pay([FromBody] PayRequest request)
        {
            // Проверка на null
            if (request == null)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);
            }

            // Валидация обязательных полей
            if (string.IsNullOrWhiteSpace(request.Login))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.LoginNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PasswordNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.OperatorNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.TxnId))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.TxnIdNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.TxnDate))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidTxnDate);
            }

            // Валидация формата даты (yyyyMMddHHmmss)
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.TxnDate, @"^\d{14}$"))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidTxnDate);
            }

            if (string.IsNullOrWhiteSpace(request.Account))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotProvided);
            }

            // Валидация формата account (только цифры)
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Account, @"^\d+$"))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidAccount);
            }

            if (string.IsNullOrWhiteSpace(request.PayerName))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PayerNameNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.Sum))
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.SumMustBeGreaterThanZero);
            }

            // Валидация суммы (должна быть больше нуля)
            if (!decimal.TryParse(request.Sum, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out decimal sumValue) || sumValue <= 0)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.SumMustBeGreaterThanZero);
            }

            // Авторизация
            var organization = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (organization == null)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AuthenticationFailed);
            }

            // Парсинг тела запроса выполняется автоматически через model binding
            // request уже содержит распарсенные данные из XML

            // Здесь будет ваша бизнес-логика для обработки платежа
            // TODO: Проверить существование аккаунта в базе данных
            // Если аккаунт не найден:
            // return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);

            // TODO: Проверить, не существует ли уже платеж с таким txn_id
            // Если платеж уже существует:
            // return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PaymentAlreadyExists);

            // Пока возвращаем заглушку успешного ответа
            var additional = new AdditionalInfo
            {
                Items = new List<AdditionalItem>
                {
                    new AdditionalItem { Name = "faculty", Value = "ФИТ" },
                    new AdditionalItem { Name = "group", Value = "Группа-1-16" },
                    new AdditionalItem { Name = "rate", Value = "1 курс" }
                }
            };

            return WebApiResponseService.CreatePaySuccessResponse(
                account: request.Account,
                walletAccount: "1256982", // Пример значения - замените на реальную логику
                service: "Плата за обучение", // Пример значения - замените на реальную логику
                payerName: request.PayerName,
                avnTxnId: "1010000000000001", // Пример значения - замените на реальную логику (генерируйте уникальный ID)
                txnId: request.TxnId,
                txnDate: request.TxnDate,
                additional: additional
            );
        }
        #endregion

        #region payInfo
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PaymentInfoResponse> payInfo([FromBody] PaymentInfoRequest request)
        {
            // Проверка на null
            if (request == null)
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.UnknownRequest);
            }

            // Валидация обязательных полей
            if (string.IsNullOrWhiteSpace(request.Login))
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.LoginNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.PasswordNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.OperatorNotProvided);
            }

            if (string.IsNullOrWhiteSpace(request.TxnId))
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.TxnIdNotProvided);
            }

            // Авторизация
            var organization = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (organization == null)
            {
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.AuthenticationFailed);
            }

            // Парсинг тела запроса выполняется автоматически через model binding
            // request уже содержит распарсенные данные из XML

            // Здесь будет ваша бизнес-логика для поиска информации о платеже по txn_id
            // TODO: Реализовать поиск платежа в базе данных по request.TxnId
            // Если платеж не найден:
            // return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.PaymentNotFound);

            // Пока возвращаем заглушку успешного ответа
            return WebApiResponseService.CreatePayInfoSuccessResponse(
                txnId: request.TxnId,
                paymentStatus: "1" // Пример значения - замените на реальную логику (1 - оплачен, 0 - не оплачен и т.д.)
            );
        }
        #endregion



    }
}
