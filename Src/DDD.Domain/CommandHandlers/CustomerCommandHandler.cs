using System;
using System.Threading;
using System.Threading.Tasks;

using DDD.Domain.Commands;
using DDD.Domain.Core.Bus;
using DDD.Domain.Core.Notifications;
using DDD.Domain.Events;
using DDD.Domain.Interfaces;
using DDD.Domain.Models;

using MediatR;

namespace DDD.Domain.CommandHandlers;

public class CustomerCommandHandler : CommandHandler,
    IRequestHandler<RegisterNewCustomerCommand, bool>,
    IRequestHandler<UpdateCustomerCommand, bool>,
    IRequestHandler<RemoveCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IMediatorHandler _bus;

    public CustomerCommandHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork uow,
        IMediatorHandler bus,
        INotificationHandler<DomainNotification> notifications)
        : base(uow, bus, notifications)
    {
        _customerRepository = customerRepository;
        _bus = bus;
    }

    public Task<bool> Handle(RegisterNewCustomerCommand message, CancellationToken cancellationToken)
    {
        if (!message.IsValid())
        {
            NotifyValidationErrors(message);
            return Task.FromResult(false);
        }

        var customer = new Customer(Guid.NewGuid(), message.Name, message.Email, message.BirthDate);

        if (_customerRepository.GetByEmail(customer.Email) != null)
        {
            _bus.RaiseEvent(new DomainNotification(message.MessageType, "The customer e-mail has already been taken."));
            return Task.FromResult(false);
        }

        _customerRepository.Add(customer);

        if (Commit())
        {
            _bus.RaiseEvent(new CustomerRegisteredEvent(customer.Id, customer.Name, customer.Email, customer.BirthDate));
        }

        return Task.FromResult(true);
    }

    public Task<bool> Handle(UpdateCustomerCommand message, CancellationToken cancellationToken)
    {
        if (!message.IsValid())
        {
            NotifyValidationErrors(message);
            return Task.FromResult(false);
        }

        var customer = new Customer(message.Id, message.Name, message.Email, message.BirthDate);
        var existingCustomer = _customerRepository.GetByEmail(customer.Email);

        if (existingCustomer != null && existingCustomer.Id != customer.Id)
        {
            if (!existingCustomer.Equals(customer))
            {
                _bus.RaiseEvent(new DomainNotification(message.MessageType, "The customer e-mail has already been taken."));
                return Task.FromResult(false);
            }
        }

        _customerRepository.Update(customer);

        if (Commit())
        {
            _bus.RaiseEvent(new CustomerUpdatedEvent(customer.Id, customer.Name, customer.Email, customer.BirthDate));
        }

        return Task.FromResult(true);
    }

    public Task<bool> Handle(RemoveCustomerCommand message, CancellationToken cancellationToken)
    {
        if (!message.IsValid())
        {
            NotifyValidationErrors(message);
            return Task.FromResult(false);
        }

        _customerRepository.Remove(message.Id);

        if (Commit())
        {
            _bus.RaiseEvent(new CustomerRemovedEvent(message.Id));
        }

        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _customerRepository.Dispose();
    }
}
