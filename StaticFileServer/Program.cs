// See https://aka.ms/new-console-template for more information
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static HttpListener _listener = new HttpListener();
    private static CancellationTokenSource _cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        // Set up signal handling for graceful shutdown
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true; // Prevent immediate termination
            _cts.Cancel();
            Console.WriteLine("Shutdown signal received...");
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
            _cts.Cancel();
        };

        // Resolve the Docs path
        string docsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Docs");
        
        // Use command line argument if provided, otherwise use Docs folder
        string servingPath = args.Length > 0 ? args[0] : docsPath;
        
        if (!Directory.Exists(servingPath))
        {
            Console.WriteLine($"Error: Directory not found: {servingPath}");
            return;
        }

        _listener.Prefixes.Add("http://localhost:8000/");
        
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            // Fallback
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:8000/");
            _listener.Start();
        }

        Console.WriteLine($"Static File Server running at http://localhost:8000/");
        Console.WriteLine($"Serving files from: {servingPath}");
        Console.WriteLine("Press Ctrl+C to stop gracefully...");

        try
        {
            await RunServerAsync(servingPath, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shutdown initiated...");
        }
        finally
        {
            await ShutdownServerAsync();
        }
    }

    private static async Task HandleRequest(HttpListenerContext context, string rootPath)
    {
        try
        {
            var response = context.Response;
            string relativePath = context.Request.Url.AbsolutePath.Trim('/');
            
            // Default to index.html
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "migration-lifecycle-documentation.html";

            string filePath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Security: prevent directory traversal
            string normalizedPath = Path.GetFullPath(filePath);
            string normalizedRoot = Path.GetFullPath(rootPath);
            
            if (!normalizedPath.StartsWith(normalizedRoot))
            {
                response.StatusCode = 403;
                response.Close();
                return;
            }

            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes("File not found");
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
                return;
            }

            // Determine content type
            string ext = Path.GetExtension(filePath).ToLower();
            string contentType = ext switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".md" => "text/markdown",
                ".txt" => "text/plain",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };

            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            response.ContentType = contentType;
            response.ContentLength64 = fileBytes.Length;
            response.StatusCode = 200;
            
            await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
            response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task RunServerAsync(string servingPath, CancellationToken cancellationToken)
    {
        var activeRequests = new List<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a request with timeout to allow cancellation checking
                var getContextTask = _listener.GetContextAsync();
                var completedTask = await Task.WhenAny(getContextTask, Task.Delay(1000, cancellationToken));

                if (completedTask == getContextTask)
                {
                    var context = await getContextTask;
                    
                    // Handle request asynchronously
                    var requestTask = Task.Run(() => HandleRequest(context, servingPath), cancellationToken);
                    activeRequests.Add(requestTask);
                    
                    // Clean up completed requests
                    activeRequests.RemoveAll(task => task.IsCompleted);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }

        // Wait for active requests to complete (with timeout)
        if (activeRequests.Count > 0)
        {
            Console.WriteLine($"Waiting for {activeRequests.Count} active requests to complete...");
            await Task.WhenAny(Task.WhenAll(activeRequests), Task.Delay(5000, CancellationToken.None));
        }
    }

    private static async Task ShutdownServerAsync()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
            Console.WriteLine("Server shut down gracefully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during shutdown: {ex.Message}");
        }
    }
}
