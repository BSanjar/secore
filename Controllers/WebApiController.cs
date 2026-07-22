using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text;
using System.Transactions;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;
using System.Globalization;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApplication1.Controllers
{
    [Route("avnWebApi/[action]")]
    [ApiController]
    [ServiceFilter(typeof(WebApplication1.Filters.XmlValidationFilter))]
    public class WebApiController : ControllerBase
    {

        private readonly AppDbContext _db;
        private readonly WebApiAuthService _authService;
        private readonly OperationsByInvoices _oper;
        private readonly ILogger<WebApiController> _logger;

        public WebApiController(
            AppDbContext db,
            WebApiAuthService authService,
            OperationsByInvoices oper,
            ILogger<WebApiController> logger)
        {
            _db = db;
            _authService = authService;
            _oper = oper;
            _logger = logger;
        }


        #region checkOld
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<CheckResponse> checkOld([FromBody] CheckRequest request)
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

           

            var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (agent == null)
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AuthenticationFailed);

            // Получаем организацию из реквизита (первые 5 цифр)
            var organizationId = request.Account.Substring(0, 5);
            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);

            if (organization == null)
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotFound);


            // Парсинг тела запроса выполняется автоматически через model binding
            // request уже содержит распарсенные данные из XML


            // Загружаем все актуальные инвойсы + графики платежей
            var invoices = await _db.Invoices
                .Include(i => i.InvoicePayments)
                .Where(i => i.PayCode == request.Account
                         && i.InvoiceStatus == "actual"
                         && i.ClientNavigation.Organization == organization.Id)
                .ToListAsync();

            if (!invoices.Any())
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotFound);


            //есть ли актуальные счета
            if (invoices.Count == 0) 
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
                    new AdditionalItem { Name = "client_adres", Value = client.ClientAddress },
                    new AdditionalItem { Name = "client_phone", Value = client.ClientPhone },
                    new AdditionalItem { Name = "client_email", Value = client.ClientEmail },
                }

                };



                string service = "";
                string nextPayDate = "";
                string fixedSumm = "";

                //счета где есть долги
                string due_invoices_pay = "СЧЕТА С ЗАДОЛЖЕННОСТЯМИ\n";

                //счета где есть оплата за сегодня
                string plan_invoices_pay = "СЧЕТА ДЛЯ ОПЛАТЫ НА ЗАВТРА\n";

                //если оплата просто на будущее
                string future_invoices_pay = "БЛИЖАЙШИЕ СЧЕТА ДЛЯ ОПЛАТЫ \n";

                string ivoisecForPay = "";

                decimal? recomendedSum = 0;
                decimal? fixedSum = 0;


               
                    //все платежи счетов где есть оплата на сегодня и ниже (долги)
                var duePayments = invoices.SelectMany(_oper.GetDuePayments);
                if (duePayments.Any())
                {
                    
                    //если есть долги
                    due_invoices_pay = string.Join("; ",
                                            duePayments
                                                .GroupBy(p => p.InvoiceNavigation)
                                                .Select(g =>
                                                {
                                                    var totalAmount = g.Sum(p => p.PaymentSumm ?? 0);

                                                    return $"{g.Key.NameInvoice}: " +
                                                           $"за {string.Join(", ", g.Select(p => p.PeriodValue))}: " +
                                                           $"Сумма(KGS): {ParsersHelper.ToMoneyStringFromCents(totalAmount)}";
                                                })
                                        );
                    ivoisecForPay = ivoisecForPay + due_invoices_pay;
                }


                    //все платежи счетов где есть оплата на завтра (плановые)
                    var duePaymentsPlan = invoices.SelectMany(_oper.GetDuePaymentsbyPlan);
                if (duePaymentsPlan.Any()) 
                {
                    plan_invoices_pay = string.Join("; ",
                                           duePaymentsPlan
                                               .GroupBy(p => p.InvoiceNavigation)
                                               .Select(g =>
                                               {
                                                   var totalAmount = g.Sum(p => p.PaymentSumm ?? 0);

                                                   return $"{g.Key.NameInvoice}: " +
                                                          $"за {string.Join(", ", g.Select(p => p.PeriodValue))}: " +
                                                          $"Сумма(KGS): {ParsersHelper.ToMoneyStringFromCents(totalAmount)}";
                                               })
                                       );

                    ivoisecForPay = ivoisecForPay+"\n" + due_invoices_pay;
                }

                    recomendedSum += duePayments.Sum(a => a.PaymentSumm ?? 0);
                    recomendedSum += duePaymentsPlan.Sum(a => a.PaymentSumm ?? 0);


                    //если долгов нет и по плану тоже нету, то рекомендуется оплатить сумму выставленных счетов на будущее
                    if (!duePayments.Any() && !duePaymentsPlan.Any())
                    {
                        //все платежи счетов где есть оплата в будущем(через день и выше)
                        var duePaymentsFuture = invoices.SelectMany(_oper.GetDuePaymentsFuture);

                        future_invoices_pay = string.Join("; ",
                                               duePaymentsPlan
                                                   .GroupBy(p => p.InvoiceNavigation)
                                                   .Select(g =>
                                                   {
                                                       var totalAmount = g.Sum(p => p.PaymentSumm ?? 0);

                                                       return $"{g.Key.NameInvoice}: " +
                                                              $"за {string.Join(", ", g.Select(p => p.PeriodValue))}: " +
                                                              $"Сумма(KGS): {ParsersHelper.ToMoneyStringFromCents(totalAmount)}";
                                                   })
                                           );
                    ivoisecForPay = ivoisecForPay + "\n" + future_invoices_pay;
                    recomendedSum += duePaymentsFuture.Sum(a => a.PaymentSumm ?? 0);
                    }


                //баланс счета\счетов               
                additional.Items.Add(new AdditionalItem { Name = "balanceSum", Value = ParsersHelper.ToMoneyStringFromCents(invoices?.FirstOrDefault()?.Balance) });

                //счета для оплаты               
                additional.Items.Add(new AdditionalItem { Name = "invoicesForPayment", Value = ivoisecForPay });


                //рекомендуемая сумма к оплате               
                additional.Items.Add(new AdditionalItem { Name = "recomendedPaySum", Value = ParsersHelper.ToMoneyStringFromCents(recomendedSum) });


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

        #region check
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<CheckResponse> check([FromBody] CheckRequest request)
        {
            _logger.LogInformation(
                "Connector check start. Account={Account} Operator={Operator}",
                request?.Account, request?.Operator);

            // 1. Базовая валидация
            if (request == null)
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.UnknownRequest);

            if (string.IsNullOrWhiteSpace(request.Login))
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.LoginNotProvided, request.Account);

            if (string.IsNullOrWhiteSpace(request.Password))
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.PasswordNotProvided, request.Account);

            if (string.IsNullOrWhiteSpace(request.Operator))
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.OperatorNotProvided, request.Account);

            if (string.IsNullOrWhiteSpace(request.Account))
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotProvided);

            if (!Regex.IsMatch(request.Account, @"^\d+$"))
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.InvalidAccount, request.Account);

            // 2. Авторизация агента
            var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (agent == null)
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AuthenticationFailed);

            // 3. Определяем организацию
            var organizationId = request.Account[..5];

            var organization = await _db.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (organization == null)
                return WebApiResponseService.CreateCheckErrorResponse(ErrorCode.AccountNotFound);

            // 4. Загружаем инвойсы + платежи
            var invoices = await _db.Invoices
                .Include(i => i.InvoicePayments)
                .Include(i => i.ClientNavigation)
                .Where(i =>
                    i.PayCode == request.Account &&
                    i.InvoiceStatus == "actual" &&
                    i.ClientNavigation.Organization == organization.Id)
                .ToListAsync();

            if (!invoices.Any())
                return WebApiResponseService.CreateCheckErrorResponse(
                    ErrorCode.AccountNotFound, request.Account);

            // 5. Клиент
            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == invoices.First().Client);

            // 6. Additional info
            var additional = new AdditionalInfo
            {
                Items = new List<AdditionalItem>
                {
                    new() { Name = "client_inn", Value = client?.ClientInn },
                    new() { Name = "client_adres", Value = client?.ClientAddress },
                    new() { Name = "client_phone", Value = client?.ClientPhone },
                    new() { Name = "client_email", Value = client?.ClientEmail }
                }
            };

            var sb = new StringBuilder();
            decimal recommendedSum = 0;

            // === ДОЛГИ ===
            var duePayments = invoices.SelectMany(_oper.GetDuePayments).ToList();
            if (duePayments.Any())
            {
                sb.AppendLine("Задолженности:");
                sb.AppendLine(_oper.BuildInvoicesText(duePayments));
                recommendedSum += duePayments.Sum(p => p.PaymentSumm ?? 0);
            }

            // === ПЛАНОВЫЕ (ЗАВТРА) ===
            var planPayments = invoices.SelectMany(_oper.GetDuePaymentsbyPlan).ToList();
            if (planPayments.Any())
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("Платежи на завтра:");
                sb.AppendLine(_oper.BuildInvoicesText(planPayments));
                recommendedSum += planPayments.Sum(p => p.PaymentSumm ?? 0);
            }

            // === БУДУЩИЕ ===
            if (!duePayments.Any() && !planPayments.Any())
            {
                var futurePayments = invoices.SelectMany(_oper.GetDuePaymentsFuture).ToList();
                if (futurePayments.Any())
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("Ближайшие платежи:");
                    sb.AppendLine(_oper.BuildInvoicesText(futurePayments));
                    recommendedSum += futurePayments.Sum(p => p.PaymentSumm ?? 0);
                }
            }

            // Учёт текущего положительного баланса при расчёте рекомендуемой суммы:
            // если на балансе уже есть деньги, вычитаем их из суммы к оплате (но не уходим в минус).
            var currentBalance = invoices.FirstOrDefault()?.Balance ?? 0m;
            if (currentBalance > 0)
            {
                recommendedSum -= currentBalance;
                if (recommendedSum < 0)
                    recommendedSum = 0;
            }

            // 7. Баланс
            additional.Items.Add(new AdditionalItem
            {
                Name = "balanceSum",
                Value = ParsersHelper.ToMoneyStringFromCents(
                    invoices.FirstOrDefault()?.Balance)
            });

            // 8. Счета для оплаты
            additional.Items.Add(new AdditionalItem
            {
                Name = "invoicesForPayment",
                Value = sb.ToString()
            });

            // 9. Рекомендуемая сумма
            additional.Items.Add(new AdditionalItem
            {
                Name = "recomendedPaySum",
                Value = ParsersHelper.ToMoneyStringFromCents(recommendedSum)
            });

            // 10. Ответ
            return WebApiResponseService.CreateCheckSuccessResponse(
                account: request.Account,
                walletAccount: "1256982",
                service: string.Empty,
                payerName: client?.ClientName,
                additional: additional
            );
        }
        #endregion

        #region payOld
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PayResponse> payOld([FromBody] PayRequest request)
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
            var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (agent == null)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AuthenticationFailed);
            }

            //из реквизита вытаскиваю id организации ()
            string organizationId = request.Account.Substring(0, 5);
            var organization = await _db.Organizations.FirstOrDefaultAsync(a => a.Id == organizationId);

            //если нету такой организации
            if (organization == null || organization.Id == null)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);
            }

            List<Invoice> invoices = new List<Invoice>();
            invoices = await _db.Invoices.Where(
                a => a.PayCode == request.Account &&
                a.InvoiceStatus == "actual" &&
                a.ClientNavigation.Organization == organization.Id).ToListAsync();

            //есть ли актуальные счета
            if (invoices.Count == 0)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);
            }

            //есть ли транзакция от этого агента с таким же txn_id в таблице transactions
            Models.DBModels.Transaction trn = await _db.Transactions.
                FirstOrDefaultAsync(a => a.TxnId == request.TxnId
                                      && a.Agent == agent.ApiLogin);

            if (trn != null && trn.Id != null)
            {
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.DuplicateCancelledTxnId);
            }


            //сумму беру в тыйынах
            decimal? requestSum = ParsersHelper.FromMoneyStringToCents(request?.Sum);

            //если сразу несколько инвойсов с таким реквизитом (т.е связанные инвойсы)
            if (invoices.Count > 1)
            {
                Invoice firstInv = invoices[0];
                //если есть долги
                if (firstInv.Balance < 0)
                {
                    //если сумма в запросе равно или больше суммы долгов
                    if (requestSum >= (firstInv.Balance * -1))
                    {
                        //остаток, для баланса
                        decimal? toBalance = requestSum;

                        //1.погашение долгов по каждому счету
                        foreach (var inv in invoices)
                        {
                            //прохожу по каждому  не закрытому графику где дата графика уже наступило 
                            foreach (var payment in inv.InvoicePayments.Where(a => a.PaymentStatus == "non_paid"
                                                                                && a.DateFrom?.Date <= DateTime.Now.Date))
                            {
                                var paymentSumm = payment.PaymentSumm ?? 0;
                                toBalance = toBalance - paymentSumm;

                                //обновляю payment.PaymentStatus
                                payment.PaymentStatus = "paid";

                                //добавляю запись в transactions (с комиссией по агенту при тарифе upper/lower_commission_agent)
                                var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                    inv.Id, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                                await _db.Transactions.AddAsync(newTrn);
                            }
                        }

                        //остаток закидываю на баланс
                        if (toBalance > 0)
                        {
                            invoices.ForEach(x => x.Balance = toBalance);
                        }
                    }
                    else
                    {
                        //если сумма платежа в запросе меньше чем того что нужно гасить долги,
                        //то погашение идет по тому у кого самые старые долги
                        var sortedInvoices = invoices
                        .Where(i => i.InvoicePayments != null && i.InvoicePayments
                            .Any(a => a.PaymentStatus == "non_paid"  //все не оплаченные
                                 && a.DateFrom?.Date < DateTime.Now.Date)) // и все те где дата начала графика уже наступил
                        .OrderBy(i => i.InvoicePayments.Min(p => p.DateFrom))
                        .ToList();

                        decimal? balance = requestSum;


                        //1.погашение долгов по каждому счету
                        foreach (var inv in invoices)
                        {
                            //прохожу по каждому  не закрытому графику где дата графика уже наступило 
                            foreach (var payment in inv.InvoicePayments.Where(a => a.PaymentStatus == "non_paid"
                                                                                && a.DateFrom?.Date <= DateTime.Now.Date))
                            {
                                    var paymentSumm = payment.PaymentSumm ?? 0;
                                    if (paymentSumm < balance)
                                    {
                                        balance = balance - paymentSumm;

                                        //обновляю payment.PaymentStatus
                                        payment.PaymentStatus = "paid";

                                        //добавляю запись в transactions (с комиссией по агенту)
                                        var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                            inv.Id, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                                        await _db.Transactions.AddAsync(newTrn);
                                    }
                                    //здесь потом можно реализовать возможность погашения тех счетов на которых хватает Д\С
                                    //else
                                    //{

                                    //}
                                
                            }
                        }
                        //остаток перекидываю на баланс
                        if (balance > 0)
                        {
                            invoices.ForEach(x => x.Balance = balance);
                        }
                    }
                }
                //если нету долгов
                else
                {
                    //Если клиент просто заранее закинул, т.е нет долгов.

                    //сперва смотрю есть ли графики по счетам где нужно погасить - сегодня,
                    //график которую нужно погасить сегодня определяется так - тот график где статус = non_paid и 
                    //дата начала графика = завтра. (потом можно сделать так чтобы можно был сразу гасить графики где дата начала след графика >= завтра )
                    //если такие есть то сразу погашу.

                    var tomorrow = DateTime.Today.AddDays(1);
                    var paymentInvoicesForTomorrow = invoices
                        .Where(i => i.InvoicePayments != null)
                        .SelectMany(i => i.InvoicePayments)
                        .Where(p => p.PaymentStatus == "non_paid" &&
                                    p.DateFrom?.Date == tomorrow.Date)
                        .ToList();

                    //если нету долгов то балансу + сумму из запроса и начинаю гасить, остаток перекину обратно на баланс
                    decimal? toBalance = requestSum + firstInv.Balance;

                    if (paymentInvoicesForTomorrow != null && paymentInvoicesForTomorrow.Count > 0)
                    {
                        foreach (var payment in paymentInvoicesForTomorrow)
                        {
                            var paymentSumm = payment.PaymentSumm ?? 0;
                            if (toBalance >= paymentSumm)
                            {
                                //обновляю payment.PaymentStatus
                                payment.PaymentStatus = "paid";

                                //добавляю запись в transactions (с комиссией по агенту)
                                var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                    payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                                await _db.Transactions.AddAsync(newTrn);

                                toBalance = toBalance - paymentSumm;
                            }                            
                        }
                    }

                    //если после погашения всех тех кого нужно было сегодня и остается сумма для баланса, 
                    //или таких не было и остается сумма для баланса - то закидываю сумму на баланс.
                    if (toBalance > 0)
                    {
                        invoices.ForEach(x => x.Balance = toBalance);
                    }
                }
            }
            //если инвойс только 1.
            else
            {
                Invoice firstInv = invoices[0];
                //если есть долг
                if (firstInv.Balance < 0)
                {
                    //если сумма в запросе равно или больше суммы долга
                    if (requestSum >= (firstInv.Balance * -1))
                    {
                        //вытаскиывю из графика платежи которых нужно было уже гасить
                        var nonpaidPayments = firstInv.InvoicePayments
                            .Where(a => a.PaymentStatus == "non_paid"
                                     && a.DateFrom?.Date <= DateTime.Now.Date);
                        decimal? toBalance = requestSum;

                        foreach (var payment in nonpaidPayments)
                        {
                            var paymentSumm = payment.PaymentSumm ?? 0;
                            toBalance = toBalance - paymentSumm;

                            //обновляю payment.PaymentStatus
                            payment.PaymentStatus = "paid";

                            //добавляю запись в transactions (с комиссией по агенту)
                            var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                            await _db.Transactions.AddAsync(newTrn);
                        }

                        invoices.ForEach(x => x.Balance = toBalance);
                    }
                    else
                    {
                        //вытаскиывю из графика платежи которых нужно было уже гасить по отсортированном ввиде так как
                        //начинаю гасить от меньшей суммы
                        var nonpaidPayments = firstInv.InvoicePayments
                             .Where(a => a.PaymentStatus == "non_paid"
                                      && a.DateFrom?.Date <= DateTime.Now.Date)
                             .OrderBy(a => a.PaymentSumm ?? 0);

                        decimal? toBalance = requestSum;

                        foreach (var payment in nonpaidPayments)
                        {
                            var paymentSumm = payment.PaymentSumm ?? 0;
                            //если сумма остатка хватает на закрытие графика счета на оплаты
                            if (toBalance >= paymentSumm)
                            {
                                toBalance = toBalance - paymentSumm;

                                //обновляю payment.PaymentStatus
                                payment.PaymentStatus = "paid";

                                //добавляю запись в transactions (с комиссией по агенту)
                                var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                    payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                                await _db.Transactions.AddAsync(newTrn);
                            }
                        }

                        //если при погашении для баланса останется ниже нуля то обнуляю
                        if (toBalance > 0)
                        {
                            invoices.ForEach(x => x.Balance = toBalance);
                        }
                    }
                }
                //если нету долга
                else
                {
                    var tomorrow = DateTime.Today.AddDays(1);
                    var paymentInvoicesForTomorrow = firstInv.InvoicePayments
                        .Where(p => p.PaymentStatus == "non_paid" &&
                                    p.DateFrom?.Date == tomorrow.Date)
                        .ToList();

                    decimal? toBalance = requestSum + firstInv.Balance;

                    if (paymentInvoicesForTomorrow != null && paymentInvoicesForTomorrow.Count > 0)
                    {
                        foreach (var payment in paymentInvoicesForTomorrow)
                        {
                            var paymentSumm = payment.PaymentSumm ?? 0;
                            if (toBalance >= paymentSumm)
                            {
                                //обновляю payment.PaymentStatus
                                payment.PaymentStatus = "paid";

                                //добавляю запись в transactions (с комиссией по агенту)
                                var newTrn = await _oper.CreateTransactionWithCommissionAsync(
                                    payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payPaymentInvoice");
                                await _db.Transactions.AddAsync(newTrn);

                                toBalance = toBalance - paymentSumm;
                            }
                        }
                    }

                    //если после погашения всех тех кого нужно было сегодня и остается сумма для баланса, 
                    //или таких не было и остается сумма для баланса - то закидываю сумму на баланс.
                    if (toBalance > 0)
                    {
                        invoices.ForEach(x => x.Balance = toBalance);
                    }
                }
            }


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

        #region payOld2
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PayResponse> payOld2([FromBody] PayRequest request)
        {
            // Начинаем транзакцию БД (КРИТИЧНО для финансов)
            using var dbTransaction = await _db.Database.BeginTransactionAsync();

            try
            {

                #region VALIDATION

                // Проверка входящего запроса
                if (request == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);

                if (string.IsNullOrWhiteSpace(request.Login))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.LoginNotProvided);

                if (string.IsNullOrWhiteSpace(request.Password))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PasswordNotProvided);

                if (string.IsNullOrWhiteSpace(request.Operator))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.OperatorNotProvided);

                if (string.IsNullOrWhiteSpace(request.TxnId))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.TxnIdNotProvided);

                // Проверка формата даты yyyyMMddHHmmss
                if (string.IsNullOrWhiteSpace(request.TxnDate) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(request.TxnDate, @"^\d{14}$"))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidTxnDate);

                // Проверка реквизита
                if (string.IsNullOrWhiteSpace(request.Account) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(request.Account, @"^\d+$"))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidAccount);

                if (string.IsNullOrWhiteSpace(request.PayerName))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PayerNameNotProvided);

                // Проверка суммы
                if (!decimal.TryParse(
                    request.Sum,
                    System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sum) || sum <= 0)
                {
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.SumMustBeGreaterThanZero);
                }

                #endregion

                #region AUTHORIZATION

                // Авторизация агента
                var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
                if (agent == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AuthenticationFailed);

                #endregion

                #region DATA_LOADING

                // Получаем организацию из реквизита (первые 5 цифр)
                var organizationId = request.Account.Substring(0, 5);
                var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);

                if (organization == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);

                // Загружаем все актуальные инвойсы + графики платежей
                var invoices = await _db.Invoices
                    .Include(i => i.InvoicePayments)
                    .Where(i => i.PayCode == request.Account
                             && i.InvoiceStatus == "actual"
                             && i.ClientNavigation.Organization == organization.Id)
                    .ToListAsync();

                if (!invoices.Any())
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);

                // Проверка на дубликат TxnId
                var duplicateTxn = await _db.Transactions
                    .AnyAsync(t => t.TxnId == request.TxnId && t.Agent == agent.Id);

                if (duplicateTxn)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.DuplicateCancelledTxnId);

                #endregion

                decimal? requestSum = ParsersHelper.FromMoneyStringToCents(request.Sum);
                var firstInvoice = invoices.First();

                #region PAYMENT_LOGIC

                // запись о принятом платеже чз API:
                _db.Transactions.Add(
                    _oper.CreateTransaction(null,null, agent, requestSum, requestSum, request.TxnId, "payFromAPI"));



                // ЕСЛИ ЕСТЬ ДОЛГ
                if (firstInvoice.Balance < 0)
                {
                    // Все просроченные платежи по всем счетам
                    var duePayments = invoices
                        .SelectMany(_oper.GetDuePayments)
                        .ToList();

                    var totalDebt = duePayments.Sum(p => p.PaymentSumm ?? 0);

                    // Денег хватает на все долги
                    if (requestSum >= totalDebt)
                    {
                        var rest = requestSum;

                        foreach (var invoice in invoices)
                            rest = await _oper.PayPaymentsAsync(_oper.GetDuePayments(invoice), rest, agent, request.TxnId);

                        // Остаток закидываем на баланс
                        if (rest > 0)
                            invoices.ForEach(i => i.Balance = rest);
                    }
                    // Денег не хватает — гасим по самым старым долгам
                    else
                    {
                        var sortedInvoices = invoices
                            .Where(i => _oper.GetDuePayments(i).Any())
                            .OrderBy(i => _oper.GetDuePayments(i).Min(p => p.DateFrom))
                            .ToList();

                        var rest = requestSum;

                        foreach (var invoice in sortedInvoices)
                            rest = await _oper.PayPaymentsAsync(_oper.GetDuePayments(invoice), rest, agent, request.TxnId);

                        if (rest > 0)
                            invoices.ForEach(i => i.Balance = rest);
                    }
                }
                // ЕСЛИ ДОЛГОВ НЕТ
                else
                {
                    var tomorrow = DateTime.Today.AddDays(1);

                    // Платежи, которые нужно оплатить завтра
                    var tomorrowPayments = invoices
                        .SelectMany(i => i.InvoicePayments)
                        .Where(p => p.PaymentStatus == "non_paid"
                                 && p.DateFrom.HasValue
                                 && p.DateFrom.Value.Date == tomorrow)
                        .ToList();

                    var rest = requestSum + firstInvoice.Balance;

                    foreach (var payment in tomorrowPayments)
                    {
                        var paymentSumm = payment.PaymentSumm ?? 0;
                        if (rest < paymentSumm)
                            break;

                        rest -= paymentSumm;
                        payment.PaymentStatus = "paid";

                        var trn = await _oper.CreateTransactionWithCommissionAsync(
                            payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, request.TxnId, "payFromAPI");
                        _db.Transactions.Add(trn);
                    }

                    if (rest > 0)
                        invoices.ForEach(i => i.Balance = rest);
                }

                #endregion

                // СОХРАНЯЕМ ВСЕ ИЗМЕНЕНИЯ
                await _db.SaveChangesAsync();

                // Фиксируем транзакцию
                await dbTransaction.CommitAsync();

                return WebApiResponseService.CreatePaySuccessResponse(
                    account: request.Account,
                    walletAccount: "1256982",
                    service: "Плата за обучение",
                    payerName: request.PayerName,
                    avnTxnId: Guid.NewGuid().ToString(),
                    txnId: request.TxnId,
                    txnDate: request.TxnDate,
                    additional: null
                );
            }
            catch (Exception ex)
            {
                // Откат транзакции при любой ошибке
                await dbTransaction.RollbackAsync();

                //_logger.LogError(ex, "Pay failed. TxnId={TxnId}", request?.TxnId);

                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);
            }
        }

        #endregion

        #region pay
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PayResponse> pay([FromBody] PayRequest request)
        {
            await using var dbTransaction = await _db.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation(
                    "Connector pay start. Account={Account} TxnId={TxnId} Operator={Operator} Sum={Sum}",
                    request?.Account, request?.TxnId, request?.Operator, request?.Sum);

                #region VALIDATION

                if (request == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);

                if (string.IsNullOrWhiteSpace(request.Login))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.LoginNotProvided);

                if (string.IsNullOrWhiteSpace(request.Password))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PasswordNotProvided);

                if (string.IsNullOrWhiteSpace(request.Operator))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.OperatorNotProvided);

                if (string.IsNullOrWhiteSpace(request.TxnId))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.TxnIdNotProvided);

                if (string.IsNullOrWhiteSpace(request.TxnDate) ||
                    !Regex.IsMatch(request.TxnDate, @"^\d{14}$"))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidTxnDate);

                if (string.IsNullOrWhiteSpace(request.Account) ||
                    !Regex.IsMatch(request.Account, @"^\d+$"))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.InvalidAccount);

                if (string.IsNullOrWhiteSpace(request.PayerName))
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.PayerNameNotProvided);

                if (!decimal.TryParse(
                    request.Sum,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var sum) || sum <= 0)
                {
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.SumMustBeGreaterThanZero);
                }

                #endregion

                #region AUTHORIZATION

                var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
                if (agent == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AuthenticationFailed);

                #endregion

                #region DATA_LOADING

                var organizationId = request.Account[..5];

                var organization = await _db.Organizations
                    .FirstOrDefaultAsync(o => o.Id == organizationId);

                if (organization == null)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);

                var invoices = await _db.Invoices
                    .Include(i => i.InvoicePayments)
                    .Include(i => i.ClientNavigation)
                    .Where(i =>
                        i.PayCode == request.Account &&
                        i.InvoiceStatus == "actual" &&
                        i.ClientNavigation.Organization == organization.Id)
                    .ToListAsync();

                if (!invoices.Any())
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.AccountNotFound);

                var duplicateTxn = await _db.Transactions
                    .AnyAsync(t => t.TxnId == request.TxnId && t.Agent == agent.Id);

                if (duplicateTxn)
                    return WebApiResponseService.CreatePayErrorResponse(ErrorCode.DuplicateCancelledTxnId);

                #endregion

                decimal requestSum = ParsersHelper.FromMoneyStringToCents(request.Sum) ?? 0m;

                // Получаем клиента для ответа
                var client = await _db.OrganizationClients
                    .FirstOrDefaultAsync(c => c.Id == invoices.First().Client);

                #region PAYMENT_LOGIC

                var paidPayments = new List<InvoicePayment>();
                decimal paidSum = 0m;
                decimal balanceAdded = 0m;
                decimal lackSum = 0m;

                var oldBalanceForAll = invoices.First().Balance ?? 0m;
                // Всегда учитываем накопленный баланс (включая положительный),
                // чтобы следующий платеж мог погасить долг за счет balance + paySum.
                // Если баланс отрицательный — это уменьшает доступную сумму (долг).
                decimal rest = requestSum + oldBalanceForAll;

                // Фиксируем входящий платёж
                _db.Transactions.Add(
                    _oper.CreateTransaction(null, null, agent, requestSum, requestSum, request.TxnId, "payFromAPI")
                );

                // Вспомогательная функция для погашения платежей (с расчётом комиссии по агенту).
                // requireFullAmount: если true — гасим платёж только при полной сумме (как в payold2);
                // иначе остаток уходит на баланс, payment_invoice не трогаем.
                async Task PayPaymentsListAsync(IEnumerable<InvoicePayment> paymentsToPay, bool requireFullAmount = false)
                {
                    foreach (var payment in paymentsToPay)
                    {
                        if (rest <= 0)
                            break;

                        var paymentAmount = payment.PaymentSumm ?? 0;
                        if (paymentAmount <= 0)
                            continue;

                        var toPay = requireFullAmount ? paymentAmount : Math.Min(rest, paymentAmount);
                        rest -= toPay;
                        payment.PaymentStatus = "paid";

                        paidPayments.Add(payment);
                        paidSum += toPay;

                        var trn = await _oper.CreateTransactionWithCommissionAsync(
                            payment.Invoice,
                            payment.Id,
                            agent,
                            toPay,
                            toPay,
                            request.TxnId,
                            "payPaymentInvoice");
                        _db.Transactions.Add(trn);
                    }
                }

                var firstInvoice = invoices.First();

                // ===== ПОСЛЕДОВАТЕЛЬНОЕ ПОГАШЕНИЕ: ДОЛГИ → ПЛАНОВЫЕ =====
                
                // 1. Сначала гасим долги (если есть) — только полной суммой, иначе остаток на баланс
                var duePayments = invoices.SelectMany(_oper.GetDuePayments).ToList();
                if (duePayments.Any())
                {
                    await PayPaymentsListAsync(duePayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
                }

                // 3. Если после погашения долгов остались средства, гасим плановые (завтра)
                // Только при полной сумме — иначе не гасим payment_invoice, остаток на баланс (как в payold2)
                var planPayments = invoices.SelectMany(_oper.GetDuePaymentsbyPlan).ToList();
                if (planPayments.Any() && rest > 0)
                {
                    await PayPaymentsListAsync(planPayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
                }

                // Будущие платежи не гасятся - остаток переводится на баланс

                // ===== ОСТАТОК =====
                // Всегда фиксируем новый баланс (может быть >0, =0 или <0)
                invoices.ForEach(i => i.Balance = rest);
                balanceAdded = rest - oldBalanceForAll;
                if (rest < 0)
                    lackSum = Math.Abs(rest);

                #endregion

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                #region RESPONSE

                var additional = new AdditionalInfo
                {
                    Items = new List<AdditionalItem>
                    {
                        // Данные клиента
                        new() { Name = "client_inn", Value = client?.ClientInn ?? string.Empty },
                        new() { Name = "client_adres", Value = client?.ClientAddress ?? string.Empty },
                        new() { Name = "client_phone", Value = client?.ClientPhone ?? string.Empty },
                        new() { Name = "client_email", Value = client?.ClientEmail ?? string.Empty },
                        
                        // Баланс счета после операции
                        new()
                        {
                            Name = "balanceSum",
                            Value = ParsersHelper.ToMoneyStringFromCents(invoices.FirstOrDefault()?.Balance)
                        },
                        
                        // Информация о погашенных платежах
                        new()
                        {
                            Name = "paidInvoices",
                            Value = paidPayments.Any() 
                                ? string.Join("\n",
                                    paidPayments
                                        .Select(p =>
                                            $"Инвойс: {p.InvoiceNavigation?.NameInvoice}, " +
                                            $"Период: {p.PeriodValue ?? "не указан"}, " +
                                            $"Сумма: {ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm)} KGS (оплачено)"
                                        ))
                                : string.Empty
                        },
                        new()
                        {
                            Name = "paidSum",
                            Value = ParsersHelper.ToMoneyStringFromCents(paidSum)
                        },
                        
                        // Информация об остатке, ушедшем на баланс
                        new()
                        {
                            Name = "balanceAdded",
                            Value = ParsersHelper.ToMoneyStringFromCents(balanceAdded)
                        },
                        
                        // Сообщение об остатке на балансе
                        new()
                        {
                            Name = "balanceMessage",
                            Value = balanceAdded > 0 
                                ? $"Остаток в размере {ParsersHelper.ToMoneyStringFromCents(balanceAdded)} переведен на баланс счета"
                                : string.Empty
                        },
                        
                        // Информация о недостающей сумме (если была)
                        new()
                        {
                            Name = "lackSum",
                            Value = ParsersHelper.ToMoneyStringFromCents(lackSum)
                        }
                    }
                };

                _logger.LogInformation(
                    "Connector pay success. Account={Account} TxnId={TxnId} PaidSum={PaidSum} BalanceAdded={BalanceAdded}",
                    request.Account, request.TxnId, paidSum, balanceAdded);

                return WebApiResponseService.CreatePaySuccessResponse(
                    account: request.Account,
                    walletAccount: "1256982",
                    service: string.Empty,
                    payerName: client?.ClientName ?? request.PayerName,
                    avnTxnId: Guid.NewGuid().ToString(),
                    txnId: request.TxnId,
                    txnDate: request.TxnDate,
                    additional: additional
                );

                #endregion
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex,
                    "Connector pay failed. Account={Account} TxnId={TxnId}",
                    request?.Account, request?.TxnId);
                return WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);
            }
        }
        #endregion

        #region payInfo
        [HttpPost]
        [Consumes("application/xml", "text/xml")]
        [Produces("application/xml")]
        public async Task<PaymentInfoResponse> payInfo([FromBody] PaymentInfoRequest request)
        {
            // 1. Базовая валидация
            if (request == null)
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.UnknownRequest);

            if (string.IsNullOrWhiteSpace(request.Login))
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.LoginNotProvided);

            if (string.IsNullOrWhiteSpace(request.Password))
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.PasswordNotProvided);

            if (string.IsNullOrWhiteSpace(request.Operator))
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.OperatorNotProvided);

            if (string.IsNullOrWhiteSpace(request.TxnId))
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.TxnIdNotProvided);

            // 2. Авторизация агента
            var agent = await _authService.AuthorizeAsync(request.Login, request.Password);
            if (agent == null)
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.AuthenticationFailed);

            // 3. Поиск транзакции по txn_id и агенту
            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t => 
                    t.TxnId == request.TxnId && 
                    t.Agent == agent.Id &&
                    t.TransactionSystem == "secore");

            // 4. Если транзакция не найдена
            if (transaction == null)
                return WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.PaymentNotFound);

            // 5. Проверка статуса транзакции
            // "1" - успешно (success), "0" - ошибка или другой статус
            var paymentStatus = transaction.TransactionStatus == "success" ? "1" : "0";

            // 6. Возвращаем информацию о платеже
            return WebApiResponseService.CreatePayInfoSuccessResponse(
                txnId: request.TxnId,
                paymentStatus: paymentStatus
            );
        }
        #endregion

    }
}
