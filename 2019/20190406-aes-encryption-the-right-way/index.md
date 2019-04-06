# 对称加密正确的使用方法
经常我看到项目中有人使用了对称加密算法，用来加密客户或项目传输中的部分数据。但我注意到开发 人员由于不熟悉原理，或者简单复制网上的代码示例，有导致代码存在安全风险。

我经常遇到的问题，有如下：
* 如使用了过时的加密算法（如DES）
* 设置了不安全的加密模式（ECB）
* 不正确地处理初始向量（IV）

## 对称加密算法
| 算法 | 位长 | 建议 |
|------|------|------|
| RC4  | 40   |      |
| DES  | 56   |      |
| 3DES | 112  |      |
| AES  | 128  | ✔    |
TL;DR:

RC4/DES/3DES都 **不符合** 加密/破解的安全性要求。

DES是56位加密，听起来感觉3DES应该是168位，但实际上其有效加密位长只有112位。

其它更长的加密算法，如AES 192位/AES 256位也符合要求。

## 加密模式
TL;DR: 不要使用ECB。

ECB不需要初始向量（IV），这个“惊人”的发现常常让开发简单粗暴地设计为ECB。ECB的问题在于输入和输出存在非常明显的关联，攻击者可以从输出轻松地猜出输入数据。
![ECB加密](1.png)

C#的AES算法默认模式为CBC，该算法没有上述的安全问题，而且最为通用，可以使用该模式。

## 初始向量
TL;DR:
初始向量 **必须** 为完全随机数，完全随机数应该使用`RandomNumberGenerator`进行加密。

回想这个问题，数据加密完后，该发送什么给接收方？仅数据？那么初始向量（IV）怎么办？

大多数开发选择的办法是，写一个固定的初始向量（IV）用于加密，然后解密时，也使用相同的初始向量。这样就导致**相同的输入会产生相同的输出**。

为什么**相同的输入应该产生不同的输出**？因为根据历史经验，攻击者可以获取一些信息，知道某个确定输入的含义。一旦再次捕获到相同的加密数据，就能轻易破解。

所以，发送数据应该包含：版本+初始向量+数据。

## 面向字符串
加密是面向字节还是字符串？我认为应该面向字节。如果面向字符串，那么很多问题很难受到重视。

试着回答这个问题：
* 用户的密码是什么样子的？
* 是长度为固定32位的HEX字符吗？如`1C8F7B2C9759209C6ACC3C105D39BBAC`？
* 还是用户想输入什么就输入什么？如`My-Super-Str0ng-Password!!`？

我认为加密算法应该面向字节流/字节数据，而不是字符串。将字符串发送给客户、放在JSON中进行端对端传输，是没什么毛病的做法。但基于以下原因，我强烈建议加密/解密算法要基于字节数据：
* 避免密码太长或太短的问题
* 来回转换为字符串效率低下
* 字符串转换为字节数组容易，其它数据序列化为字节数据也容易

## 我的加密/解密方法
```C#
string Encrypt(string password, string purpose, byte[] plainBytes)
{
	byte[] key = PasswordToKey(password, purpose);
	using (var aes = Aes.Create())
	{
		aes.Key = key;
		using (ICryptoTransform encryptor = aes.CreateEncryptor())
		{
			byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
			byte[] packedBytes = Pack(
				version: 1, 
				iv: aes.IV, 
				cipherBytes: cipherBytes);
			return Base64UrlEncode(packedBytes);
		}
	}
}

byte[] Decrypt(string packedString, string password, string purpose)
{
	byte[] key = PasswordToKey(password, purpose);
	byte[] packedBytes = Base64UrlDecode(packedString);
	(byte version, byte[] iv, byte[] cipherBytes) = Unpack(packedBytes);
	using (var aes = Aes.Create())
	{
		using (ICryptoTransform decryptor = aes.CreateDecryptor(key, iv))
		{
			return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
		}
	}
}
```

其中公共方法：
```C#
byte[] PasswordToKey(string password, string purpose)
{
	using (var hmac = new HMACMD5(Encoding.UTF8.GetBytes(purpose)))
	{
		return hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
	}
}

string Base64UrlEncode(byte[] bytes)
{
	return Convert.ToBase64String(bytes)
			.Replace("/", "_")
			.Replace("+", "-")
			.Replace("=", "");
}

byte[] Base64UrlDecode(string base64Url)
{
	return Convert.FromBase64String(base64Url
		.Replace("_", "/")
		.Replace("-", "+"));
}

(byte version, byte[] iv, byte[] cipherBytes) Unpack(byte[] packedBytes)
{
	if (packedBytes[0] == 1)
	{
		// version 1
		return (1, packedBytes[1..1 + 16], packedBytes[1 + 16..]);
	}
	else
	{
		throw new NotImplementedException("unknown version");
	}
}

byte[] Pack(byte version, byte[] iv, byte[] cipherBytes)
{
	return new[] { version }.Concat(iv).Concat(cipherBytes).ToArray();
}
```
解释:
* Base64UrlEncode/Decode：用于将字符串在Url上传输，将`+/=`转换成：`-_`
* Pack/Unpack：将版本/初始向量/密文打包/解包
* PasswordToKey：将长度不一样密码，加上`purpose`，转换为长度一样的`key`，其中改成HMACSHA256可以使用256位的AES算法。

测试代码：
```C#
string purpose = "这个算法是用来搞SSO的";
// 返回：AcfCe3AQcmNkeNThv-u09H_HyGKy_iRy-7uGiW0IZOHI
Encrypt("密码here", purpose, Encoding.UTF8.GetBytes("Hello World"));
// 返回：Hello World
Encoding.UTF8.GetString(Decrypt("AcfCe3AQcmNkeNThv-u09H_HyGKy_iRy-7uGiW0IZOHI", "密码here", purpose));
```