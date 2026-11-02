using System.Diagnostics.CodeAnalysis;

namespace CleanSlices.Core.Shared;

public class Result
{
	public bool IsSuccess { get; }
	public bool IsFailure => !IsSuccess;
	public Error? Error { get; }

	protected Result(bool isSuccess, Error? error)
	{
		if (isSuccess && error is not null || !isSuccess && error is null)
		{
			throw new InvalidOperationException("A successful result cannot have an error, and a failed result must have one.");
		}

		IsSuccess = isSuccess;
		Error = error;
	}

	public static Result Success() => new(true, null);
	public static Result Failure(Error error) => new(false, error);
	public static Result<TValue> Success<TValue>(TValue value) => new(true, null, value);
	public static Result<TValue> Failure<TValue>(Error error) => new(false, error, default);
	protected static readonly Error NullValueError = new("Null Value", "Result value cannot be null");
}

public class Result<TValue> : Result
{
	private readonly TValue? _value;
	[NotNull]
	public TValue Value => IsSuccess
		? _value!
		: throw new InvalidOperationException("Cannot access the value of a failed result.");

	public Result(bool isSuccess, Error? error, TValue? value) : base(isSuccess, error)
	{
		_value = value;
	}

	public static implicit operator Result<TValue>(TValue value) => value is not null
		? Success(value)
		: Failure<TValue>(NullValueError);
}

public static class ResultExtensions
{
	extension<TIn, TOut>(Result<TIn> result)
	{

		public Result<TOut> Bind(Func<TIn, Result<TOut>> next) =>
		result.IsFailure ? Result.Failure<TOut>(result.Error!) : next(result.Value);

		public Result<TOut> Map(Func<TIn, TOut> map) =>
		result.IsFailure ? Result.Failure<TOut>(result.Error!) : Result.Success(map(result.Value));
	}

	extension<TIn>(Result<TIn> result)
	{
		public Result Bind(Func<TIn, Result> next) =>
		result.IsFailure ? Result.Failure(result.Error!) : next(result.Value);

		public void Match(
				Action<TIn> onSuccess,
				Action<Error> onFailure)
		{
			if (result.IsSuccess)
			{
				onSuccess(result.Value);
				return;
			}
			onFailure(result.Error!);
		}

		public TOut Match<TOut>(
				Func<TIn, TOut> onSuccess,
				Func<Error, TOut> onFailure) =>
			result.IsSuccess ? onSuccess(result.Value)
				: onFailure(result.Error!);

		public Result<TIn> Tap(Action<TIn> action)
		{
			if (result.IsSuccess) action(result.Value);
			return result;
		}

		public Result<TIn> TapError(Action<Error> action)
		{
			if (result.IsFailure) action(result.Error!);
			return result;
		}

		public async Task<Result<TIn>> TapAsync(Func<TIn, Task> action)
		{
			if (result.IsSuccess) await action(result.Value);
			return result;
		}

		public async Task<Result<TIn>> TapErrorAsync(Func<Error, Task> action)
		{
			if (result.IsFailure) await action(result.Error!);
			return result;
		}
	}


	extension(Result result)
	{
		public Result Bind(Func<Result> next) =>
			result.IsFailure ? Result.Failure(result.Error!) : next();

		public void Match(
				Action onSuccess,
				Action<Error> onFailure)
		{
			if (result.IsSuccess)
			{
				onSuccess();
				return;
			}
			onFailure(result.Error!);
		}

		public TOut Match<TOut>(
				Func<TOut> onSuccess,
				Func<Error, TOut> onFailure) =>
			result.IsSuccess ? onSuccess()
				: onFailure(result.Error!);

		public Result Tap(Action action)
		{
			if (result.IsSuccess) action();
			return result;
		}

		public Result TapError(Action action)
		{
			if (result.IsFailure) action();
			return result;
		}

		public async Task<Result> TapAsync(Func<Task> action)
		{
			if (result.IsSuccess) await action();
			return result;
		}

		public async Task<Result> TapErrorAsync(Func<Task> action)
		{
			if (result.IsFailure) await action();
			return result;
		}
	}
}
