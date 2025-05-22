const amqp = require('amqplib');

// RabbitMQ connection configuration - match with your consumer settings
const config = {
    protocol: 'amqp',
    hostname: 'localhost',
    port: 5672,
    username: 'user',
    password: 'password',
    vhost: '/',
    exchange: 'piranha.external.events',
    queue: 'piranha.external.queue', // Match your C# queue name
    routingKey: 'content.create'
};

async function publishMessage() {
    console.log('Piranha Events Publisher');
    console.log('=======================');
    console.log(`Connecting to RabbitMQ at ${config.hostname}:${config.port}...`);
    
    try {
        // Create connection
        const connection = await amqp.connect({
            protocol: config.protocol,
            hostname: config.hostname,
            port: config.port,
            username: config.username,
            password: config.password,
            vhost: config.vhost
        });

        // Create channel
        const channel = await connection.createChannel();
        
        // Setup exchange, queue, and binding (matching C# service setup)
        await channel.assertExchange(config.exchange, 'direct', { durable: false });
        await channel.assertQueue(config.queue, { durable: false });
        await channel.bindQueue(config.queue, config.exchange, config.routingKey);
        
        // Message to publish
        const message = {
            title: "Test Article",
            description: "This is a test",
            body: "<p>Hello from external publisher</p>"
        };
        
        // Publish the message
        const success = channel.publish(
            config.exchange,
            config.routingKey,
            Buffer.from(JSON.stringify(message)),
            { 
                contentType: 'application/json',
                messageId: `msg-${Date.now()}`,
                timestamp: Math.floor(Date.now() / 1000),
                headers: { 
                    Source: "external-publisher"
                }
            }
        );
        
        if (success) {
            console.log('Message published successfully:');
            console.log(JSON.stringify(message, null, 2));
        } else {
            console.error('Failed to publish message');
        }
        
        // Increase delay to ensure message delivery
        setTimeout(async () => {
            await channel.close();
            await connection.close();
            console.log('Connection closed');
        }, 1000); // Longer delay to ensure message is sent
        
    } catch (error) {
        console.error('Error:', error.message);
        console.error(error.stack);
        process.exit(1);
    }
}

// Run the publisher
publishMessage();