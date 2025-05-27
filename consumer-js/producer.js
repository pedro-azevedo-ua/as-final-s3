const amqp = require('amqplib');
const crypto = require('crypto');
const readline = require('readline');

const SIGNING_KEY = 'as-2025-123951';

const config = {
    protocol: 'amqp',
    hostname: 'localhost',
    port: 5672,
    username: 'user',
    password: 'password',
    vhost: '/',
    exchange: 'piranha.external.events',
    queue: 'piranha.external.queue', // Match your C# queue name
    routingKeyCreate: 'content.create',
    routingKeyDelete: 'content.delete'
};

function createSignature(payload, key) {
    return crypto
        .createHmac('sha256', key)
        .update(payload)
        .digest('hex');
}

async function sendMessage(routingKey, message) {
    const conn = await amqp.connect({
        protocol: config.protocol,
        hostname: config.hostname,
        port: config.port,
        username: config.username,
        password: config.password,
        vhost: config.vhost
    });
    const channel = await conn.createChannel();

   await channel.assertExchange(config.exchange, 'direct', { durable: false });
   await channel.assertQueue(config.queue, { durable: false });
   await channel.bindQueue(config.queue, config.exchange, config.routingKey);
        

    channel.publish(
        config.exchange,
        routingKey,
        Buffer.from(JSON.stringify(message)),
        {
            persistent: true,
            contentType: 'application/json'
        }
    );

    console.log(`Message sent with routing key "${routingKey}"`);
    await channel.close();
    await conn.close();
}

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});
function ask(question) {
    return new Promise(resolve => rl.question(question, answer => resolve(answer)));
}

async function sendCreateMessageInteractive() {
    const title = await ask('Enter page title: ');
    const slug = await ask('Enter page slug: ');
    const authorName = await ask('Enter author name: ');
    const authorEmail = await ask('Enter author email: ');
    const regionMain = await ask('Enter main region content: ');

    const payload = {
        id: crypto.randomUUID(),
        name: "CreateContent",
        createdAt: new Date().toISOString(),
        content: {
            title: title,
            slug: slug,
            type: "StandardPage",
            regions: {
                main: regionMain
            }
        },
        author: {
            name: authorName,
            email: authorEmail
        },
        hashedUserId: crypto.createHash('sha256').update(authorEmail).digest('hex')
    };

    const jsonToSign = JSON.stringify(payload);
    // console.log('JS JSON to sign:', jsonToSign);
    const signature = createSignature(jsonToSign, SIGNING_KEY);

    payload.Signature = signature;
    // console.log(JSON.stringify(payload, null, 2));

    await sendMessage(config.routingKeyCreate, payload);
}

async function sendDeleteMessageInteractive() {
    const title = await ask('Enter the title of the page to delete: ');
    const authorName = await ask('Enter author name: ');
    const authorEmail = await ask('Enter author email: ');

    const payload = {
        Id: crypto.randomUUID(),
        Name: "DeleteContent",
        CreatedAt: new Date().toISOString(),
        Content: {
            Title: title
        },
        Author: {
            Name: authorName,
            Email: authorEmail
        },
        HashedUserId: crypto.createHash('sha256').update(authorEmail).digest('hex')
    };

    const signature = createSignature(JSON.stringify(payload), SIGNING_KEY);
    payload.Signature = signature;

    await sendMessage(config.routingKeyDelete, payload);
}

async function main() {
    while (true) {
        console.log('\nWhat do you want to do?');
        console.log('1. Create a page');
        console.log('2. Delete a page');
        console.log('3. Exit');
        const choice = await ask('Choose an option (1/2/3): ');

        if (choice === '1') {
            await sendCreateMessageInteractive();

        } else if (choice === '2') {
            await sendDeleteMessageInteractive();
        } else if (choice === '3') {
            console.log('Goodbye!');
            rl.close();
            process.exit(0);
        } else {
            console.log('Invalid option. Try again.');
        }
    }

}

main();
