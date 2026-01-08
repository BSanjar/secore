namespace WebApplication1.Models.WebApiModels
{
    /// <summary>
    /// Коды ошибок для API ответов
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// Неизвестный запрос
        /// </summary>
        UnknownRequest = -1,

        /// <summary>
        /// Успешно
        /// </summary>
        Success = 0,

        /// <summary>
        /// Значение для параметра account не представлено
        /// </summary>
        AccountNotProvided = 10,

        /// <summary>
        /// Значение для параметра sum должно быть больше нуля
        /// </summary>
        SumMustBeGreaterThanZero = 12,

        /// <summary>
        /// Значение для параметра txn_id не представлено
        /// </summary>
        TxnIdNotProvided = 13,

        /// <summary>
        /// Значение для параметра txn_date имеет неверное значение
        /// </summary>
        InvalidTxnDate = 14,

        /// <summary>
        /// Неверное значение для параметра account
        /// </summary>
        InvalidAccount = 15,

        /// <summary>
        /// Запрашиваемая транзакция для отмены не найдена
        /// </summary>
        TransactionNotFoundForCancel = 16,

        /// <summary>
        /// Запрашиваемая транзакция найдена, но была отменена ранее
        /// </summary>
        TransactionAlreadyCancelled = 17,

        /// <summary>
        /// Значение для параметра payer_name не представлено
        /// </summary>
        PayerNameNotProvided = 18,

        /// <summary>
        /// Запрашиваемый аккаунт не найден
        /// </summary>
        AccountNotFound = 19,

        /// <summary>
        /// Значение для поля operator не представлено
        /// </summary>
        OperatorNotProvided = 20,

        /// <summary>
        /// Значение для поля login не представлено
        /// </summary>
        LoginNotProvided = 30,

        /// <summary>
        /// Значение для поля password не представлено
        /// </summary>
        PasswordNotProvided = 31,

        /// <summary>
        /// Значение для поля comment не представлено
        /// </summary>
        CommentNotProvided = 32,

        /// <summary>
        /// Была произведена попытка повторной регистрации оплаты на txn_id, который уже был отменён
        /// </summary>
        DuplicateCancelledTxnId = 33,

        /// <summary>
        /// Значение для поля id_agent не представлено
        /// </summary>
        IdAgentNotProvided = 34,

        /// <summary>
        /// Значение для поля phone_number не представлено
        /// </summary>
        PhoneNumberNotProvided = 35,

        /// <summary>
        /// Не верный ввод номера телефона
        /// </summary>
        InvalidPhoneNumber = 36,

        /// <summary>
        /// Запрашиваемая услуга не поддерживается данным методом
        /// </summary>
        ServiceNotSupported = 37,

        /// <summary>
        /// Оплата уже существует с отправленным txn_id
        /// </summary>
        PaymentAlreadyExists = 38,

        /// <summary>
        /// Оплата с переданным txn_id не найдена
        /// </summary>
        PaymentNotFound = 39,

        /// <summary>
        /// Системная ошибка
        /// </summary>
        SystemError = 100,

        /// <summary>
        /// Аутентификация не пройдена
        /// </summary>
        AuthenticationFailed = 200
    }
}

