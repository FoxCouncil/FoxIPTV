// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using System;

    public class Result : Tuple<bool, string>
    {
        private static readonly Result _successResult = new Result(true, "Success");

        public bool IsSuccess => Item1;

        public string Message => Item2;

        public Result(bool success, string message) : base(success, message) { }

        public static Result Success() => _successResult;

        public static Result Success(string successMessage) => new Result(true, successMessage);

        public static Result Failure(string failureMessage) => new Result(false, failureMessage);

    }
}
