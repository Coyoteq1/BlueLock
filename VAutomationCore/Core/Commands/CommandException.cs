using System;

namespace VAutomationCore.Core.Commands
{
    /// <summary>
    /// Exception thrown when a command encounters an error.
    /// This exception is caught by CommandBase and displayed to the user.
    /// </summary>
    public class CommandException : Exception
    {
        /// <summary>
        /// Creates a new CommandException with the specified message.
        /// </summary>
        /// <param name="message">The error message to display to the user.</param>
        public CommandException(string message) : base(message)
        {
        }
        
        /// <summary>
        /// Creates a new CommandException with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public CommandException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
