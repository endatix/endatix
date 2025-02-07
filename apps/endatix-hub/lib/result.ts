type Success<T> = {
  kind: Kind.Success;
  value: T;
};

type Error = {
  kind: Kind.Error;
  errorType: ErrorType;
  message: string;
  details?: string;
};

type Result<T> = Success<T> | Error;

enum Kind {
  Success,
  Error,
}

enum ErrorType {
  ValidationError,
  Error,
}

const Result = {
  success: <T>(value: T): Success<T> => ({
    kind: Kind.Success,
    value,
  }),

  error: <T>(message: string, details?: string): Result<T> => ({
    kind: Kind.Error,
    errorType: ErrorType.Error,
    message,
    details,
  }),

  validationError: <T>(message: string, details?: string): Result<T> => ({
    kind: Kind.Error,
    errorType: ErrorType.ValidationError,
    message,
    details,
  }),

  isSuccess: <T>(result: Result<T>): result is Success<T> =>
    result.kind === Kind.Success,

  isError: <T>(result: Result<T>): result is Error =>
    result.kind === Kind.Error,
};

export type { Result as ResultType, Success, Error };

export { Kind, ErrorType, Result };
