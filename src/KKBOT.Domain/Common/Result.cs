// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value. Throws if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the operation failed.</exception>
    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("Cannot access Value on a failed result.");
            }
            return _value!;
        }
    }

    /// <summary>
    /// Gets the error. Throws if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the operation succeeded.</exception>
    public Error Error
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("Cannot access Error on a successful result.");
            }
            return _error ?? throw new InvalidOperationException("Error not set on failed result.");
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Maps the success value to a new type.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error ?? throw new InvalidOperationException("Error not set on failed result."));
    }

    /// <summary>
    /// Maps the success value to a new result type.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error ?? throw new InvalidOperationException("Error not set on failed result."));
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(_value!);
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (IsFailure)
        {
            action(_error ?? throw new InvalidOperationException("Error not set on failed result."));
        }
        return this;
    }

    /// <summary>
    /// Matches the result and returns a value.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error ?? throw new InvalidOperationException("Error not set on failed result."));
    }

    public bool Equals(Result<T> other)
    {
        if (IsSuccess != other.IsSuccess) return false;
        if (IsSuccess) return EqualityComparer<T>.Default.Equals(_value!, other._value!);
        return Equals(_error, other._error);
    }

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (IsSuccess) return _value?.GetHashCode() ?? 0;
        return _error?.GetHashCode() ?? 0;
    }

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}

/// <summary>
/// Represents the result of an operation that can succeed or fail (non-generic).
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the error. Throws if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the operation succeeded.</exception>
    public Error Error
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("Cannot access Error on a successful result.");
            }
            return _error ?? throw new InvalidOperationException("Error not set on failed result.");
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
        {
            action();
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<Error> action)
    {
        if (IsFailure)
        {
            action(_error ?? throw new InvalidOperationException("Error not set on failed result."));
        }
        return this;
    }

    /// <summary>
    /// Matches the result and returns a value.
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(_error ?? throw new InvalidOperationException("Error not set on failed result."));
    }

    public bool Equals(Result other) => _isSuccess == other._isSuccess && Equals(_error, other._error);
    public override bool Equals(object? obj) => obj is Result other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_isSuccess, _error);
    public static bool operator ==(Result left, Result right) => left.Equals(right);
    public static bool operator !=(Result left, Result right) => !left.Equals(right);
}
