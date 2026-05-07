using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Validators
{
    public class DateRangeAttribute : ValidationAttribute
    {
        private readonly string _startPropertyName;

        public DateRangeAttribute(string startPropertyName)
        {
            _startPropertyName = startPropertyName;
            ErrorMessage = "Čas začátku nesmí být vyšší než čas konce";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            var endDate = value as DateTime?;

            var startProperty = context.ObjectType.GetProperty(_startPropertyName);
            var startDate = startProperty?.GetValue(context.ObjectInstance) as DateTime?;

            if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate)
                return new ValidationResult(ErrorMessage);

            return ValidationResult.Success;
        }
    }
}
