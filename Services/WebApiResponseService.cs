using WebApplication1.Models.WebApiModels;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для формирования стандартизированных ответов API
    /// </summary>
    public class WebApiResponseService
    {
        /// <summary>
        /// Получить описание ошибки по коду
        /// </summary>
        private static string GetErrorDescription(ErrorCode errorCode)
        {
            return errorCode switch
            {
                ErrorCode.UnknownRequest => "Неизвестный запрос",
                ErrorCode.Success => "ОК",
                ErrorCode.AccountNotProvided => "Значение для параметра account не представлено",
                ErrorCode.SumMustBeGreaterThanZero => "Значение для параметра sum должно быть больше нуля",
                ErrorCode.TxnIdNotProvided => "Значение для параметра txn_id не представлено",
                ErrorCode.InvalidTxnDate => "Значение для параметра txn_date имеет неверное значение, формат даты должен быть виде: yyyyMMddHHmmss, например, (20160101153028)",
                ErrorCode.InvalidAccount => "Неверное значение для параметра account, формат аккаунта должен быть виде: XXXXXXXXXXX {…} (только цифры)",
                ErrorCode.TransactionNotFoundForCancel => "Запрашиваемая транзакция для отмены не найдена",
                ErrorCode.TransactionAlreadyCancelled => "Запрашиваемая транзакция найдена, но была отменена ранее",
                ErrorCode.PayerNameNotProvided => "Значение для параметра payer_name не представлено",
                ErrorCode.AccountNotFound => "Запрашиваемый аккаунт account не найден",
                ErrorCode.OperatorNotProvided => "Значение для поля operator не представлено",
                ErrorCode.LoginNotProvided => "Значение для поля login не представлено",
                ErrorCode.PasswordNotProvided => "Значение для поля password не представлено",
                ErrorCode.CommentNotProvided => "Значение для поля comment не представлено",
                ErrorCode.DuplicateCancelledTxnId => "Была произведена попытка повторной регистрации оплаты на txn_id, который уже был отменён",
                ErrorCode.IdAgentNotProvided => "Значение для поля id_agent не представлено",
                ErrorCode.PhoneNumberNotProvided => "Значение для поля phone_number не представлено",
                ErrorCode.InvalidPhoneNumber => "Не верный ввод номера телефона",
                ErrorCode.ServiceNotSupported => "Запрашиваемая услуга не поддерживается данным методом",
                ErrorCode.PaymentAlreadyExists => "Оплата уже существует с отправленным txn_id",
                ErrorCode.PaymentNotFound => "Оплата с переданным txn_id не найдена.",
                ErrorCode.SystemError => "Системная ошибка",
                ErrorCode.AuthenticationFailed => "Аутентификация не пройдена",
                _ => "Неизвестная ошибка"
            };
        }

        /// <summary>
        /// Создать успешный ответ для метода check
        /// </summary>
        public static CheckResponse CreateCheckSuccessResponse(
            string account,
            string walletAccount,
            string service,
            string payerName,
            AdditionalInfo? additional = null)
        {
            return new CheckResponse
            {
                Account = account,
                WalletAccount = walletAccount,
                Service = service,
                Result = (int)ErrorCode.Success,
                Description = GetErrorDescription(ErrorCode.Success),
                Additional = additional,
                PayerName = payerName
            };
        }

        /// <summary>
        /// Создать ответ с ошибкой для метода check
        /// </summary>
        public static CheckResponse CreateCheckErrorResponse(
            ErrorCode errorCode,
            string? account = null)
        {
            return new CheckResponse
            {
                Account = account ?? string.Empty,
                Result = (int)errorCode,
                Description = GetErrorDescription(errorCode)
            };
        }

        /// <summary>
        /// Создать успешный ответ для метода pay
        /// </summary>
        public static PayResponse CreatePaySuccessResponse(
            string account,
            string walletAccount,
            string service,
            string payerName,
            string avnTxnId,
            string txnId,
            string txnDate,
            AdditionalInfo? additional = null)
        {
            return new PayResponse
            {
                Account = account,
                WalletAccount = walletAccount,
                Service = service,
                Result = (int)ErrorCode.Success,
                Description = GetErrorDescription(ErrorCode.Success),
                Additional = additional,
                PayerName = payerName,
                AvnTxnId = avnTxnId,
                TxnId = txnId,
                TxnDate = txnDate
            };
        }

        /// <summary>
        /// Создать ответ с ошибкой для метода pay
        /// </summary>
        public static PayResponse CreatePayErrorResponse(ErrorCode errorCode)
        {
            return new PayResponse
            {
                Result = (int)errorCode,
                Description = GetErrorDescription(errorCode)
            };
        }

        /// <summary>
        /// Создать успешный ответ для метода payInfo
        /// </summary>
        public static PaymentInfoResponse CreatePayInfoSuccessResponse(
            string txnId,
            string paymentStatus)
        {
            return new PaymentInfoResponse
            {
                Result = (int)ErrorCode.Success,
                Description = GetErrorDescription(ErrorCode.Success),
                TxnId = txnId,
                PaymentStatus = paymentStatus
            };
        }

        /// <summary>
        /// Создать ответ с ошибкой для метода payInfo
        /// </summary>
        public static PaymentInfoResponse CreatePayInfoErrorResponse(ErrorCode errorCode)
        {
            return new PaymentInfoResponse
            {
                Result = (int)errorCode,
                Description = GetErrorDescription(errorCode)
            };
        }
    }
}

