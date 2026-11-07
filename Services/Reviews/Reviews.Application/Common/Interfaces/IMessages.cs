using MediatR;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Команда без результату (зміна стану).</summary>
public interface ICommand : IRequest { }

/// <summary>Команда, що повертає результат (наприклад, Id створеного документа).</summary>
public interface ICommand<out TResponse> : IRequest<TResponse> { }

/// <summary>Запит на читання; стан не змінює.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse> { }
