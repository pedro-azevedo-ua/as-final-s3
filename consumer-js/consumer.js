const amqp = require('amqplib');

// RabbitMQ connection configuration - match with your Docker settings
const config = {
    protocol: 'amqp',
    hostname: 'localhost',
    port: 5672,
    username: 'user',
    password: 'password',
    vhost: '/',
    exchange: 'piranha.events',
    queue: 'piranha.events.queue',
    routingKey: 'content.test' // Match the routing key you're using in C#
};

async function consumeMessages() {
    console.log('Piranha Events Consumer');
    console.log('======================');
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
        
        // Setup exchange, queue and binding
        await channel.assertExchange(config.exchange, 'direct', { durable: false });
        await channel.assertQueue(config.queue, { durable: false });
        await channel.bindQueue(config.queue, config.exchange, config.routingKey);
        
        console.log(` [*] Connected to RabbitMQ. Waiting for messages on queue '${config.queue}'`);
        console.log(` [*] To exit press CTRL+C`);
        
        // Start consuming messages
        await channel.consume(config.queue, (msg) => {
            if (msg !== null) {
                try {
                    // Get message content and properties
                    const content = msg.content.toString();
                    const routingKey = msg.fields.routingKey;
                    const exchange = msg.fields.exchange;
                    const contentType = msg.properties.contentType;
                    const messageId = msg.properties.messageId || 'Not specified';
                    
                    console.log('\n==================================');
                    console.log(`Received message from exchange: ${exchange}`);
                    console.log(`Routing key: ${routingKey}`);
                    console.log(`Content type: ${contentType || 'Not specified'}`);
                    console.log(`Message ID: ${messageId}`);
                    
                    // Safely handle timestamp
                    if (msg.properties.timestamp) {
                        try {
                            const date = new Date(msg.properties.timestamp * 1000);
                            console.log(`Timestamp: ${date.toISOString()}`);
                        } catch (err) {
                            console.log(`Timestamp: Invalid (${msg.properties.timestamp})`);
                        }
                    } else {
                        console.log('Timestamp: Not specified');
                    }
                    
                    // Log headers safely
                    const headers = msg.properties.headers || {};
                    if (Object.keys(headers).length > 0) {
                        console.log('Headers:');
                        for (const [key, value] of Object.entries(headers)) {
                            // Convert Buffer to string if needed
                            let displayValue;
                            if (value instanceof Buffer) {
                                displayValue = value.toString();
                            } else if (value === null || value === undefined) {
                                displayValue = 'null';
                            } else {
                                displayValue = value;
                            }
                            console.log(`  ${key}: ${displayValue}`);
                        }
                    } else {
                        console.log('Headers: None');
                    }
                    
                    // Parse and display JSON content
                    try {
                        const jsonData = JSON.parse(content);
                        console.log('Message payload (JSON):');
                        console.log(JSON.stringify(jsonData, null, 2));
                    } catch (err) {
                        console.log('Message payload (raw):');
                        console.log(content);
                    }
                    console.log('==================================\n');
                    
                    // Acknowledge message
                    channel.ack(msg);
                } catch (err) {
                    console.error('Error processing message:', err);
                    // Still acknowledge to prevent the message from being requeued if it's corrupted
                    channel.ack(msg);
                }
            }
        });
        
        // Setup graceful shutdown
        process.on('SIGINT', async () => {
            console.log('\nClosing connection...');
            try {
                await channel.close();
                await connection.close();
            } catch (err) {
                console.error('Error closing connections:', err);
            }
            process.exit(0);
        });
        
    } catch (error) {
        console.error('Error:', error.message);
        console.error(error.stack);
        process.exit(1);
    }
}

// Start the consumer
consumeMessages();