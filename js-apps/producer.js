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
    exchange: 'cms.requests',
    exchangeType: 'topic', // Match your C# setup
    routingKeyCreate: 'page.create.request',
    routingKeyDelete: 'page.delete.request'
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

    // Declare exchange as durable
    await channel.assertExchange(config.exchange, config.exchangeType, { durable: true });

    // Don't declare or bind the queue here unless you need to (let consumers handle it)

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
    const signature = createSignature(jsonToSign, SIGNING_KEY);

    payload.signature = signature;
    //print the payload to see what it looks like
    console.log('Sending payload:', JSON.stringify(payload, null, 2));
    await sendMessage(config.routingKeyCreate, payload);
}
async function sendBadMessage() {


    const payload = {
        id: crypto.randomUUID(),
        name: "CreateContent",
        createdAt: new Date().toISOString(),
        content: {
            title: "Bad Request",
            slug: "bad-request",
            type: "StandardPage",
            regions: {
                main: "This is a bad request example."
            }
        },
        author: {
            name: "Test Author",
            email: "test@author.com"
        },
        hashedUserId: crypto.createHash('sha256').update("test").digest('hex')
    };

    const jsonToSign = JSON.stringify(payload);
    const signature = createSignature(jsonToSign, 'bad-signing-key');

    payload.signature = signature;
    //print the payload to see what it looks like
    console.log('Sending bad request payload:', JSON.stringify(payload, null, 2));
    await sendMessage(config.routingKeyCreate, payload);
}
async function sendDeleteMessageInteractive() {
    const title = await ask('Enter the title of the page to delete: ');
    const authorName = await ask('Enter author name: ');
    const authorEmail = await ask('Enter author email: ');

    const payload = {
        id: crypto.randomUUID(),
        name: "DeleteContent",
        createdAt: new Date().toISOString(),
        content: {
            title: title
        },
        author: {
            name: authorName,
            email: authorEmail
        },
        hashedUserId: crypto.createHash('sha256').update(authorEmail).digest('hex')
    };

    const signature = createSignature(JSON.stringify(payload), SIGNING_KEY);
    payload.signature = signature;

    await sendMessage(config.routingKeyDelete, payload);
}

async function main() {
    while (true) {
        console.log('\nWhat do you want to do?');
        console.log('1. Create a page');
        console.log('2. Delete a page');
        console.log('3. Bad Request (simulate error)');
        console.log('4. Exit');
        const choice = await ask('Choose an option (1/2/3/4): ');

        if (choice === '1') {
            await sendCreateMessageInteractive();

        } else if (choice === '2') {
            await sendDeleteMessageInteractive();
        }
        else if (choice === '3') {
            await sendBadMessage();
        } else if (choice === '4') {
            console.log('Goodbye!');
            rl.close();
            process.exit(0);
        } else {
            console.log('Invalid option. Try again.');
        }
    }
}

main();
