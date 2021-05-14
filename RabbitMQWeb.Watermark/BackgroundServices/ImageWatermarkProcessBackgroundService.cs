using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQWeb.Watermark.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQWeb.Watermark.BackgroundServices
{
    public class ImageWatermarkProcessBackgroundService : BackgroundService  // bunu startup da haberdar etmek gerek
    {
        // bana kanal gelsin diye RabbitMQClientService alıyoruz.

        private readonly RabbitMQClientService _rabbitMQClientService;

        // bir de loglama alalım.

        private readonly ILogger<ImageWatermarkProcessBackgroundService> _logger;

        // kanalı burada oluşturuyoruz.
        private IModel _channel;

        public ImageWatermarkProcessBackgroundService(RabbitMQClientService rabbitMQClientService,ILogger<ImageWatermarkProcessBackgroundService> logger)
        {
            _rabbitMQClientService = rabbitMQClientService;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // kanalı burada alıyoruz.
            _channel = _rabbitMQClientService.Connect();

            _channel.BasicQos(0, 1, false);

            // start ettiğimizde rabbitMQ bağlanıyor. ve kaçar kaçar dağıttığımızı söylüyor.

            return base.StartAsync(cancellationToken);
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            _channel.BasicConsume(RabbitMQClientService.QueueName, false,consumer);

            consumer.Received += Consumer_Received; // eventi buradan yakalıyorz.

            return Task.CompletedTask;

        }

        private Task Consumer_Received(object sender, BasicDeliverEventArgs @event)
        {
            try
            {

                // resme imaj ekleme olayı(yazı ekleme) burada yapacağız

                var productimageCreatedEvent = JsonSerializer.Deserialize<ProductImageCreatedEvent>(Encoding.UTF8.GetString(@event.Body.ToArray())); // bu bana @event dan gelecek biz önce productImageCreatedeventini serialze edip, event ile gelen byte dizisini stringe çevircez. (wwwroot içerisindeki image path bana gelen event.)

                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Images", productimageCreatedEvent.ImageName);

                var siteName = "Latife Esra Sancak";

                using var img = Image.FromFile(path); // bana bir path ver ben sana file'i veriyim diyoruz.

                using var graphic = Graphics.FromImage(img); // grafik üzerinden bana bir imaj ver diyorum. bu imaj üzerinden artık ben resim ekleyebilirim.

                // resmin sağ alt köşesine yazı yazıyoruz.
                var font = new Font(FontFamily.GenericMonospace, 32, FontStyle.Bold, GraphicsUnit.Pixel);

                var textSize = graphic.MeasureString(siteName, font); // stringi ölçecez

                var color = Color.FromArgb(128, 255, 255, 255);

                var brush = new SolidBrush(color);  // yazı yazma işlemnini gerçekleştricez

                var position = new Point(img.Width - ((int)textSize.Width + 30), img.Height - ((int)textSize.Height + 30));// pozisyonu ayarlayacaz

                graphic.DrawString(siteName, font, brush, position);

                img.Save("wwwroot/Images/watermarks" + productimageCreatedEvent.ImageName);

                img.Dispose();
                graphic.Dispose();

                _channel.BasicAck(@event.DeliveryTag, false); // eventin tagını bildir diyorum. hata fırlatıldığında kuyruktan silinmeyecek.

            }
            catch (Exception)
            {

                throw;
            }

            return Task.CompletedTask;

            

        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }
    }
}
