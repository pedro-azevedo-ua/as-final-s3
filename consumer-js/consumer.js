const amqp = require('amqplib');

// Configuration
const config = {
    protocol: 'amqp',
    hostname: 'localhost',
    port: 5672,
    username: 'user',
    password: 'password',
    vhost: '/',
    exchange: 'cms.events',
    exchangeType: 'topic',
    queue: 'cms.requests.processor',
    routingKeys: [
        "page.published",
        "page.deleted",
        "page.draft"
    ]
};

// Parse CLI arguments for routing keys (e.g., node consumer.js page.published page.draft)
const cliKeys = process.argv.slice(2);
const listenKeys = cliKeys.length > 0 ? cliKeys : config.routingKeys;

console.log('Piranha Events Consumer');
console.log('======================');
console.log(`Connecting to RabbitMQ at ${config.hostname}:${config.port}...`);
console.log(`Will listen for event types: ${listenKeys.join(', ')}`);

async function consumeMessages() {
    try {
        // Connect
        const connection = await amqp.connect({
            protocol: config.protocol,
            hostname: config.hostname,
            port: config.port,
            username: config.username,
            password: config.password,
            vhost: config.vhost
        });

        const channel = await connection.createChannel();

        // Durable exchange and queue
        await channel.assertExchange(config.exchange, config.exchangeType, { durable: true });
        await channel.assertQueue(config.queue, {
            durable: true,
            arguments: {
                'x-dead-letter-exchange': 'cms.dlx',
                'x-dead-letter-routing-key': 'dlq' 
            }
        });


        // Bind the queue to each selected routing key
        for (const routingKey of listenKeys) {
            await channel.bindQueue(config.queue, config.exchange, routingKey);
            console.log(`Bound queue '${config.queue}' to routing key '${routingKey}'`);
        }

        console.log(` [*] Waiting for messages on queue '${config.queue}' for keys: ${listenKeys.join(', ')}`);
        console.log(` [*] To exit press CTRL+C`);

        await channel.consume(config.queue, (msg) => {
            if (msg !== null) {
                try {
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

                    // Timestamp
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

                    // Headers
                    const headers = msg.properties.headers || {};
                    if (Object.keys(headers).length > 0) {
                        console.log('Headers:');
                        for (const [key, value] of Object.entries(headers)) {
                            let displayValue = value instanceof Buffer ? value.toString() : value;
                            if (value === null || value === undefined) displayValue = 'null';
                            console.log(`  ${key}: ${displayValue}`);
                        }
                    } else {
                        console.log('Headers: None');
                    }

                    // JSON parsing
                    try {
                        const jsonData = JSON.parse(content);
                        console.log('Message payload (JSON):');
                        console.log(JSON.stringify(jsonData, null, 2));
                    } catch (err) {
                        console.log('Message payload (raw):');
                        console.log(content);
                    }
                    console.log('==================================\n');

                    channel.ack(msg);
                } catch (err) {
                    console.error('Error processing message:', err);
                    channel.ack(msg);
                }
            }
        });

        // Graceful shutdown
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

consumeMessages();
