namespace NexCalc.Core
{
    // A calculation failure carries both a machine-readable kind (for tests
    // and programmatic handling) and a user-facing message, mirroring the
    // way the Windows Calculator separates error codes from display text.
    public class ErrorType : Exception {
        public string kind;
        public string userMsg;   // user-facing text ('message' is the base Exception property)

        public ErrorType(string kind_, string userMsg_) {
            kind = kind_;
            userMsg = userMsg_;
        }

        public static ErrorType Create(string kind_, string message_) {
            return new ErrorType(kind_, message_);
        }

        public static ErrorType SyntaxError(string message_) {
            return Create("SyntaxError", message_);
        }

        public static ErrorType MissingValue() {
            return Create("MissingValue", "Value is missing.");
        }

        public static ErrorType ParenMismatch() {
            return Create("ParenMismatch", "Parenthesis mismatch: an opening parenthesis was not closed.");
        }

        public static ErrorType UnexpectedClose() {
            return Create("ParenMismatch", "Unexpected ')' - parenthesis mismatch.");
        }

        public static ErrorType EmptyExpression() {
            return Create("EmptyExpression", "Expression is empty.");
        }

        public static ErrorType UnknownIdentifier(string name) {
            return Create("UndefinedVariable", "Unknown identifier '" + name + "'.");
        }

        public static ErrorType ZeroDivision() {
            return Create("ZeroDivisionError", "Can't divide by 0.");
        }

        public static ErrorType Undefined(string message_) {
            return Create("Undefined", message_);
        }

        public static ErrorType NotANumber() {
            return Create("NotANumber", "Result is not a number.");
        }

        public static ErrorType OutOfRange() {
            return Create("OutOfRange", "The input value is out of range.");
        }

        public static ErrorType NotAnInteger() {
            return Create("NotAnInteger", "Value is not an integer.");
        }

        public static ErrorType ValueTooLarge() {
            return Create("ValueTooLarge", "Value is too large.");
        }

        public static ErrorType BadFactorial() {
            return Create("BadFactorial", "The factorial function only accepts non-negative integers.");
        }
    }
}
