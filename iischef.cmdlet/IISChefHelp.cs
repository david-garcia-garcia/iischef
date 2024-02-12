using iischef.cmdlet;
using iischef.logger;
using System;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;

[Cmdlet(VerbsLifecycle.Invoke, "IISChefHelp")]
[OutputType(typeof(string))]
public class IISChefHelp : ChefCmdletBase
{
    protected override void DoProcessRecord(ILoggerInterface logger)
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        var cmdletTypes = executingAssembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ChefCmdletBase)) && !t.IsAbstract)
            .OrderBy((i) => i.Name);

        Towel.Towel.LoadXmlDocumentation(executingAssembly);

        foreach (var cmdletType in cmdletTypes)
        {
            var cmdletAttribute = cmdletType.GetCustomAttribute<CmdletAttribute>();
            if (cmdletAttribute != null)
            {
                var cmdletName = $"{cmdletAttribute.VerbName}-{cmdletAttribute.NounName}";
                this.WriteObject(cmdletName);

                var summary = this.ExtractInnerText(Towel.Towel.GetDocumentation(cmdletType));
                if (!string.IsNullOrEmpty(summary))
                {
                    this.WriteObject($"  Summary: {this.FormatText(summary, 12)}");
                }

                var parameterProperties = cmdletType.GetProperties()
                    .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null);

                foreach (var property in parameterProperties)
                {
                    var parameterAttribute = property.GetCustomAttribute<ParameterAttribute>();
                    var parameterType = property.PropertyType.Name;
                    var parameterName = property.Name;
                    var output = $"  {parameterName} ({parameterType})";

                    if (property.PropertyType.IsEnum)
                    {
                        output += " [" + string.Join(",", Enum.GetNames(property.PropertyType)) + "]";
                    }

                    var parameterSummary = this.ExtractInnerText(Towel.Towel.GetDocumentation(property));

                    if (!string.IsNullOrEmpty(parameterSummary))
                    {
                        output += $" - {this.FormatText(parameterSummary, output.Length + 3)}";
                    }

                    if (parameterAttribute != null && parameterAttribute.Mandatory)
                    {
                        output += " [Mandatory]";
                    }

                    this.WriteObject(output);
                }

                this.WriteObject(string.Empty); // Add an empty line for better readability
            }
        }
    }

    private string ExtractInnerText(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
        {
            return xmlContent;
        }

        var regex = new Regex(@"<[^>]+>|<\/[^>]+>");
        return regex.Replace(xmlContent, string.Empty).Trim();
    }

    private string FormatText(string text, int indentation)
    {
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var formattedLines = lines.Select((line, index) => index == 0 ? line : new string(' ', indentation) + line);
        return string.Join(Environment.NewLine, formattedLines);
    }
}
